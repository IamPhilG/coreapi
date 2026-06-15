#Requires -Version 5.1
<#
.SYNOPSIS
    First-run wizard that provisions the AWS infrastructure required to run
    coreapi integration tests or a live demo against a real AD DS instance.

.DESCRIPTION
    What the wizard does, in order:
      1  Validates (and if needed re-authenticates) the AWS CLI session
      2  Asks for region and instance type
      3  Asks for the AD domain name and administrator password
      4  Optionally allocates an Elastic IP so the DC always has the same address
      5  Finds the latest Windows Server 2022 Base AMI in the chosen region
      6  Creates (or reuses) Security Group 'coreapi-test-dc' with inbound rules
         for LDAP (389), LDAPS (636), and RDP (3389) from your current public IP
      7  Asks for the run mode: test or demo
      8  Writes tests\CoreApi.IntegrationTests\appsettings.Development.json
      9  Optionally runs the integration tests immediately

    Idempotent: re-running reuses existing AWS resources and preserves the
    ExistingInstanceId written after the first test run.

.PARAMETER Profile
    Named AWS CLI profile to use (matches ~/.aws/credentials or ~/.aws/config).
    Default: 'default'.
    Examples: 'coreapi-dev', 'my-sso-profile'

    The same profile name is written to appsettings.Development.json so the
    xUnit fixture uses identical credentials.

.PARAMETER Mode
    'test' - instance stopped after each test run (cheapest)
    'demo' - instance stays running after tests, AD seeded with demo objects
    Omit to be asked interactively.

.EXAMPLE
    .\tools\setup-test-dc.ps1
    .\tools\setup-test-dc.ps1 -Profile coreapi-dev
    .\tools\setup-test-dc.ps1 -Profile my-sso -Mode demo
#>
[CmdletBinding()]
param(
    [string]$Profile = "default",

    [ValidateSet("test", "demo", "")]
    [string]$Mode = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepoRoot   = Split-Path $PSScriptRoot -Parent
$ConfigPath = Join-Path $RepoRoot "tests\CoreApi.IntegrationTests\appsettings.Development.json"

# ---------- Console helpers --------------------------------------------------

function Write-Banner([string]$text) {
    Write-Host ""
    Write-Host ("=" * 56) -ForegroundColor Green
    Write-Host "  $text" -ForegroundColor Green
    Write-Host ("=" * 56) -ForegroundColor Green
}

function Write-Step([int]$n, [int]$total, [string]$text) {
    Write-Host ""
    Write-Host "Step $n/$total -- $text" -ForegroundColor Cyan
}

function Read-WithDefault([string]$prompt, [string]$default) {
    $value = Read-Host "  $prompt [$default]"
    if ([string]::IsNullOrWhiteSpace($value)) { $default } else { $value.Trim() }
}

function Read-YesNo([string]$prompt, [bool]$defaultYes = $false) {
    $hint  = if ($defaultYes) { "Y/n" } else { "y/N" }
    $value = Read-Host "  $prompt [$hint]"
    if ([string]::IsNullOrWhiteSpace($value)) { return $defaultYes }
    return $value.Trim() -match "^[Yy]"
}

function Read-SecureString([string]$prompt) {
    $secure = Read-Host "  $prompt" -AsSecureString
    [Runtime.InteropServices.Marshal]::PtrToStringAuto(
        [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))
}

function Write-Info([string]$text) { Write-Host "  $text" -ForegroundColor DarkGray }
function Write-Ok([string]$text)   { Write-Host "  $text" -ForegroundColor Green }
function Write-Warn([string]$text) { Write-Host "  $text" -ForegroundColor Yellow }

# ---------- AWS helpers ------------------------------------------------------

function Invoke-Aws {
    param(
        [string[]]$Arguments,
        [switch]$AllowFailure
    )
    $cmdArgs = $Arguments + @("--profile", $Profile)
    $output = & aws @cmdArgs 2>&1
    if ($LASTEXITCODE -eq 0) { return $output }
    if ($AllowFailure) { return $null }
    throw "AWS CLI error (exit $LASTEXITCODE): $($output -join ' ')"
}

function Assert-AwsSession {
    Write-Info "Checking AWS session for profile '$Profile'..."

    $output = & aws sts get-caller-identity --profile $Profile --output json 2>&1
    if ($LASTEXITCODE -eq 0) {
        $id = $output | ConvertFrom-Json
        Write-Ok "Account : $($id.Account)"
        Write-Ok "Identity: $($id.Arn)"
        return
    }

    Write-Warn "Session expired or not found for profile '$Profile'."
    Write-Info "Attempting: aws sso login --profile $Profile"
    & aws sso login --profile $Profile
    if ($LASTEXITCODE -eq 0) {
        $output = & aws sts get-caller-identity --profile $Profile --output json
        if ($LASTEXITCODE -eq 0) {
            $id = $output | ConvertFrom-Json
            Write-Ok "Re-authenticated. Account: $($id.Account)"
            return
        }
    }

    Write-Host ""
    Write-Host "  Could not authenticate automatically. Run ONE of:" -ForegroundColor Red
    Write-Host "    aws sso login --profile $Profile          # IAM Identity Center" -ForegroundColor White
    Write-Host "    aws configure --profile $Profile          # Access key / secret" -ForegroundColor White
    Write-Host "  Then re-run this script." -ForegroundColor White
    exit 1
}

function Get-MyPublicIp {
    try   { (Invoke-RestMethod "https://checkip.amazonaws.com").Trim() }
    catch { Read-Host "  Could not detect your public IP. Enter it (without /32)" }
}

function Get-LatestWin2022Ami([string]$Region) {
    $output = Invoke-Aws @(
        "ec2", "describe-images",
        "--owners", "amazon",
        "--filters", "Name=name,Values=Windows_Server-2022-English-Full-Base-*",
                     "Name=state,Values=available",
        "--query", "sort_by(Images, &CreationDate)[-1].ImageId",
        "--region", $Region,
        "--output", "text"
    )
    $amiId = ($output -join "").Trim()
    if (-not $amiId -or $amiId -eq "None") {
        throw "No Windows Server 2022 AMI found in region '$Region'."
    }
    $amiId
}

function Get-OrCreateSecurityGroup([string]$Region) {
    $name = "coreapi-test-dc"
    $existing = Invoke-Aws @(
        "ec2", "describe-security-groups",
        "--filters", "Name=group-name,Values=$name",
        "--query", "SecurityGroups[0].GroupId",
        "--region", $Region,
        "--output", "text"
    ) -AllowFailure
    $existing = ($existing -join "").Trim()

    if ($existing -and $existing -ne "None") {
        Write-Info "Reusing Security Group: $existing"
        return $existing
    }

    Write-Info "Creating Security Group '$name'..."
    $raw = Invoke-Aws @(
        "ec2", "create-security-group",
        "--group-name", $name,
        "--description", "CoreAPI test/demo DC - LDAP + LDAPS + RDP",
        "--region", $Region,
        "--output", "json"
    )
    ($raw -join "" | ConvertFrom-Json).GroupId
}

function Add-InboundRule([string]$SgId, [int]$Port, [string]$Cidr, [string]$Region) {
    $prev = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    & aws ec2 authorize-security-group-ingress `
        --group-id $SgId --protocol tcp --port $Port --cidr $Cidr `
        --region $Region --profile $Profile --output json | Out-Null
    $ErrorActionPreference = $prev
    # Non-zero exit = duplicate rule = harmless
}

function New-ElasticIp([string]$Region) {
    $raw = Invoke-Aws @(
        "ec2", "allocate-address",
        "--domain", "vpc",
        "--region", $Region,
        "--output", "json"
    )
    $raw -join "" | ConvertFrom-Json
}

# ---------- Read existing config (for re-runs) --------------------------------

$prevConfig         = $null
$existingInstanceId = ""
$existingEipAllocId = ""
$isUpdate           = $false

if (Test-Path $ConfigPath) {
    try {
        $prevConfig         = Get-Content $ConfigPath -Raw | ConvertFrom-Json
        $ti                 = $prevConfig.TestInfrastructure
        $existingInstanceId = if ($ti.ExistingInstanceId) { $ti.ExistingInstanceId } else { "" }
        $existingEipAllocId = if ($ti.ElasticIpAllocationId) { $ti.ElasticIpAllocationId } else { "" }
        $isUpdate           = $true
    } catch { }
}

# ---------- Banner ------------------------------------------------------------

Write-Banner "CoreAPI -- EC2 test/demo DC setup wizard"
Write-Info "Profile: $Profile"
Write-Info "Output : $ConfigPath"
if ($existingInstanceId) { Write-Info "Existing instance: $existingInstanceId (will be preserved)" }
if ($isUpdate) {
    Write-Warn "Config file already exists. Answers below will overwrite it."
    if (-not (Read-YesNo "Continue?" $true)) { Write-Host "Aborted."; exit 0 }
}

# ---------- Step 1 -- AWS session --------------------------------------------

Write-Step 1 7 "AWS session (profile: $Profile)"
Assert-AwsSession

# ---------- Step 2 -- Region and instance type --------------------------------

Write-Step 2 7 "Region and instance type"
$defaultRegion = if ($prevConfig) { $prevConfig.TestInfrastructure.AwsRegion } else { "us-east-1" }
$defaultType   = if ($prevConfig) { $prevConfig.TestInfrastructure.InstanceType } else { "t3.medium" }

$Region       = Read-WithDefault "AWS region" $defaultRegion
$InstanceType = Read-WithDefault "Instance type (t3.medium ~`$0.075/hr)" $defaultType

# ---------- Step 3 -- AD domain -----------------------------------------------

Write-Step 3 7 "Active Directory domain"
$defaultDomain  = if ($prevConfig) { $prevConfig.TestInfrastructure.DomainName } else { "corp.local" }
$defaultNetbios = if ($prevConfig) { $prevConfig.TestInfrastructure.DomainNetbiosName } else { "CORP" }

$DomainName    = Read-WithDefault "Domain FQDN" $defaultDomain
$DomainNetbios = Read-WithDefault "NETBIOS name" $defaultNetbios

# ---------- Step 4 -- Password ------------------------------------------------

Write-Step 4 7 "AD Administrator password"
Write-Info "Min 8 chars -- uppercase + lowercase + digit + symbol."
Write-Info "Written to the local config file only (gitignored, never committed)."

$existingPwd     = if ($prevConfig) { $prevConfig.TestInfrastructure.AdAdminPassword } else { "" }
$AdAdminPassword = if ($existingPwd -and (Read-YesNo "Keep existing password?" $true)) {
    $existingPwd
} else {
    $p1 = Read-SecureString "Password"
    $p2 = Read-SecureString "Confirm"
    if ($p1 -ne $p2) { throw "Passwords do not match." }
    $p1
}

# ---------- Step 5 -- Elastic IP ----------------------------------------------

Write-Step 5 7 "Elastic IP"
Write-Info "Keeps the DC at the same address across start/stop cycles."
Write-Info "Without one the IP changes every restart -- config update needed."

$EipAllocationId = $existingEipAllocId
if ($existingEipAllocId) {
    Write-Info "Existing allocation preserved: $existingEipAllocId"
} elseif (Read-YesNo "Allocate an Elastic IP?" $true) {
    Write-Info "Allocating..."
    $eip             = New-ElasticIp $Region
    $EipAllocationId = $eip.AllocationId
    Write-Ok "Allocated: $($eip.PublicIp) ($EipAllocationId)"
}

# ---------- Step 6 -- Mode ----------------------------------------------------

Write-Step 6 7 "Run mode"
Write-Info "test - instance stopped after each test run (cheapest)"
Write-Info "demo - instance kept running + AD seeded with demo objects"

if (-not $Mode) {
    $modeInput = Read-WithDefault "Mode" "test"
    $Mode      = if ($modeInput.Trim() -eq "demo") { "demo" } else { "test" }
}

$KeepRunning  = ($Mode -eq "demo")
$SeedDemoData = ($Mode -eq "demo")
Write-Ok "Mode: $Mode (KeepRunning=$KeepRunning, SeedDemoData=$SeedDemoData)"

# ---------- Step 7 -- AWS resources -------------------------------------------

Write-Step 7 7 "Provisioning AWS resources"

Write-Info "Finding latest Windows Server 2022 AMI in $Region..."
$AmiId = Get-LatestWin2022Ami $Region
Write-Ok "AMI: $AmiId"

$SgId = Get-OrCreateSecurityGroup $Region
Write-Ok "Security Group: $SgId"

Write-Info "Detecting your public IP..."
$myIp   = Get-MyPublicIp
$myCidr = "$myIp/32"
Write-Info "Adding inbound rules for $myCidr (idempotent)..."
Add-InboundRule $SgId 389  $myCidr $Region
Add-InboundRule $SgId 636  $myCidr $Region
Add-InboundRule $SgId 3389 $myCidr $Region
Write-Ok "TCP 389 (LDAP) + 636 (LDAPS) + 3389 (RDP) from $myCidr"

# ---------- Write config ------------------------------------------------------

$config = [ordered]@{
    TestInfrastructure = [ordered]@{
        ProvisionAdDc           = $true
        AwsProfile              = $Profile
        AwsRegion               = $Region
        InstanceType            = $InstanceType
        AmiId                   = $AmiId
        SecurityGroupId         = $SgId
        SubnetId                = ""
        KeyPairName             = ""
        ElasticIpAllocationId   = $EipAllocationId
        ExistingInstanceId      = $existingInstanceId
        DomainName              = $DomainName
        DomainNetbiosName       = $DomainNetbios
        AdAdminPassword         = $AdAdminPassword
        LdapReadyTimeoutSeconds = 900
        KeepRunning             = $KeepRunning
        SeedDemoData            = $SeedDemoData
    }
}

$config | ConvertTo-Json -Depth 3 | Set-Content $ConfigPath -Encoding utf8
Write-Ok "Written: $ConfigPath"

# ---------- Summary -----------------------------------------------------------

Write-Banner "Setup complete"
$account = & aws sts get-caller-identity --profile $Profile --query Account --output text
Write-Host "  Profile       : $Profile" -ForegroundColor White
Write-Host "  Account       : $account" -ForegroundColor White
Write-Host "  Region        : $Region" -ForegroundColor White
Write-Host "  AMI           : $AmiId" -ForegroundColor White
Write-Host "  Security Group: $SgId" -ForegroundColor White
if ($EipAllocationId) { Write-Host "  Elastic IP    : $EipAllocationId" -ForegroundColor White }
Write-Host "  Mode          : $Mode" -ForegroundColor White
Write-Host ""
Write-Info "First run : ~12-15 min (Windows boot + AD DS promotion + reboot)."
Write-Info "Later runs: ~2-3 min (restart stopped instance)."
if ($Mode -eq "demo") {
    Write-Info "DC stays running after tests -- point coreapi at the IP shown in test output."
}
Write-Host ""

if (Read-YesNo "Run integration tests now?" $true) {
    Push-Location $RepoRoot
    try {
        & dotnet test tests\CoreApi.IntegrationTests --filter "Category=Integration" -v normal
        if ($LASTEXITCODE -eq 0) { Write-Ok "All integration tests passed." }
        else                     { Write-Warn "Some tests failed -- check output above." }
    } finally {
        Pop-Location
    }
} else {
    Write-Host ""
    Write-Info "When ready:"
    Write-Host "  dotnet test tests\CoreApi.IntegrationTests --filter ""Category=Integration"" -v normal" -ForegroundColor White
}
