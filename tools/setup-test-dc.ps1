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
      6  Terminates previous instance (if any) and creates/retrieves EC2 key pair
      7  Asks for the run mode: test or demo
      8  Creates Security Group with inbound rules for LDAP/LDAPS/RDP
      9  Launches new instance with key pair for RDP access
     10  Writes tests\CoreApi.IntegrationTests\appsettings.Development.json
     11  Optionally runs the integration tests immediately

.PARAMETER AwsProfile
    Named AWS CLI profile to use (matches ~/.aws/credentials or ~/.aws/config).
    Default: 'default'.
    Examples: 'coreapi-dev', 'my-sso-profile'

.PARAMETER Mode
    'test' - instance stopped after each test run (cheapest)
    'demo' - instance stays running after tests, AD seeded with demo objects
    Omit to be asked interactively.

.EXAMPLE
    .\tools\setup-test-dc.ps1
    .\tools\setup-test-dc.ps1 -AwsProfile coreapi-dev
    .\tools\setup-test-dc.ps1 -AwsProfile my-sso -Mode demo
#>
[CmdletBinding()]
param(
    [string]$AwsProfile = "default",

    [ValidateSet("test", "demo", "")]
    [string]$Mode = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepoRoot   = Split-Path $PSScriptRoot -Parent
$ConfigPath = Join-Path $RepoRoot "tests\CoreApi.IntegrationTests\appsettings.Development.json"

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

function Invoke-Aws {
    param([string[]]$Arguments, [switch]$AllowFailure)
    $cmdArgs = $Arguments + @("--profile", $AwsProfile)
    $prev = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    $output = & aws @cmdArgs 2>&1
    $ErrorActionPreference = $prev
    if ($LASTEXITCODE -eq 0) { return $output }
    if ($AllowFailure) { return $null }
    throw "AWS CLI error (exit $LASTEXITCODE): $($output -join ' ')"
}

function Assert-AwsSession {
    Write-Info "Checking AWS session for profile '$AwsProfile'..."
    $prev = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    $output = & aws sts get-caller-identity --profile $AwsProfile --output json 2>&1
    $ErrorActionPreference = $prev
    if ($LASTEXITCODE -eq 0) {
        $id = $output | ConvertFrom-Json
        Write-Ok "Account : $($id.Account)"
        Write-Ok "Identity: $($id.Arn)"
        return
    }
    Write-Warn "Session expired or not found for profile '$AwsProfile'."
    Write-Info "Attempting: aws sso login --profile $AwsProfile"
    $ErrorActionPreference = "Continue"
    & aws sso login --profile $AwsProfile
    $ErrorActionPreference = $prev
    if ($LASTEXITCODE -eq 0) {
        $ErrorActionPreference = "Continue"
        $output = & aws sts get-caller-identity --profile $AwsProfile --output json 2>&1
        $ErrorActionPreference = $prev
        if ($LASTEXITCODE -eq 0) {
            $id = $output | ConvertFrom-Json
            Write-Ok "Re-authenticated. Account: $($id.Account)"
            return
        }
    }
    Write-Host ""
    Write-Host "  Could not authenticate automatically. Run ONE of:" -ForegroundColor Red
    Write-Host "    aws sso login --profile $AwsProfile          # IAM Identity Center" -ForegroundColor White
    Write-Host "    aws configure --profile $AwsProfile          # Access key / secret" -ForegroundColor White
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
        --region $Region --profile $AwsProfile --output json | Out-Null
    $ErrorActionPreference = $prev
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

function Get-OrCreateIamRole {
    $roleName = "coreapi-test-dc-instance-role"
    Write-Info "Checking for IAM role: $roleName"

    $output = Invoke-Aws @("iam", "get-role", "--role-name", $roleName, "--output", "json") -AllowFailure
    if ($output) {
        Write-Ok "IAM role already exists: $roleName"
        return $roleName
    }

    Write-Info "Creating IAM role with SSM permissions..."
    $trustPolicy = @{
        Version = "2012-10-17"
        Statement = @(
            @{
                Effect = "Allow"
                Principal = @{ Service = "ec2.amazonaws.com" }
                Action = "sts:AssumeRole"
            }
        )
    } | ConvertTo-Json -Depth 3

    Invoke-Aws @("iam", "create-role", "--role-name", $roleName, "--assume-role-policy-document", $trustPolicy, "--output", "json") | Out-Null

    Write-Info "Attaching AmazonSSMManagedInstanceCore policy..."
    Invoke-Aws @("iam", "attach-role-policy", "--role-name", $roleName, "--policy-arn", "arn:aws:iam::aws:policy/AmazonSSMManagedInstanceCore") | Out-Null

    Write-Ok "IAM role created and configured: $roleName"
    $roleName
}

function Get-OrCreateInstanceProfile {
    param([string]$RoleName)
    $profileName = "coreapi-test-dc-instance-profile"
    Write-Info "Checking for instance profile: $profileName"

    $output = Invoke-Aws @("iam", "get-instance-profile", "--instance-profile-name", $profileName, "--output", "json") -AllowFailure
    if ($output) {
        Write-Ok "Instance profile already exists: $profileName"
        return $profileName
    }

    Write-Info "Creating instance profile..."
    Invoke-Aws @("iam", "create-instance-profile", "--instance-profile-name", $profileName, "--output", "json") | Out-Null
    Invoke-Aws @("iam", "add-role-to-instance-profile", "--instance-profile-name", $profileName, "--role-name", $RoleName) | Out-Null

    Write-Ok "Instance profile created: $profileName"
    $profileName
}

$prevConfig         = $null
$existingInstanceId = ""
$existingEipAllocId = ""
$isUpdate           = $false
$Region             = ""

if (Test-Path $ConfigPath) {
    try {
        $prevConfig         = Get-Content $ConfigPath -Raw | ConvertFrom-Json
        $ti                 = $prevConfig.TestInfrastructure
        $existingInstanceId = if ($ti.ExistingInstanceId) { $ti.ExistingInstanceId } else { "" }
        $existingEipAllocId = if ($ti.ElasticIpAllocationId) { $ti.ElasticIpAllocationId } else { "" }
        $Region             = if ($ti.AwsRegion) { $ti.AwsRegion } else { "" }
        $isUpdate           = $true
    } catch { }
}

Write-Banner "CoreAPI -- EC2 test/demo DC setup wizard"
Write-Info "Profile: $AwsProfile"
Write-Info "Output : $ConfigPath"
if ($existingInstanceId) { Write-Info "Existing instance: $existingInstanceId (will be terminated)" }
if ($isUpdate) {
    Write-Warn "Config file already exists. Answers below will overwrite it."
    if (-not (Read-YesNo "Continue?" $true)) { Write-Host "Aborted."; exit 0 }
}

Write-Step 1 11 "AWS session (profile: $AwsProfile)"
Assert-AwsSession

Write-Step 2 11 "Region and instance type"
$defaultRegion = if ($prevConfig) { $prevConfig.TestInfrastructure.AwsRegion } else { "us-east-1" }
$defaultType   = if ($prevConfig) { $prevConfig.TestInfrastructure.InstanceType } else { "t3.medium" }

$Region       = Read-WithDefault "AWS region" $defaultRegion
$InstanceType = Read-WithDefault "Instance type (t3.medium ~`$0.075/hr)" $defaultType

Write-Step 3 11 "Active Directory domain"
$defaultDomain  = if ($prevConfig) { $prevConfig.TestInfrastructure.DomainName } else { "corp.local" }
$defaultNetbios = if ($prevConfig) { $prevConfig.TestInfrastructure.DomainNetbiosName } else { "CORP" }

$DomainName    = Read-WithDefault "Domain FQDN" $defaultDomain
$DomainNetbios = Read-WithDefault "NETBIOS name" $defaultNetbios

Write-Step 4 10 "AD Administrator password"
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

Write-Step 5 11 "Elastic IP"
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

Write-Step 6 11 "Terminate previous instance"

# If we have an Elastic IP but no instance ID, query AWS for the associated instance
if ($existingEipAllocId -and -not $existingInstanceId -and $Region) {
    Write-Info "Checking for instance associated with Elastic IP: $existingEipAllocId"
    $output = Invoke-Aws @(
        "ec2", "describe-addresses",
        "--allocation-ids", $existingEipAllocId,
        "--query", "Addresses[0].InstanceId",
        "--region", $Region,
        "--output", "text"
    ) -AllowFailure
    $output = ($output -join "").Trim()
    if ($output -and $output -ne "None") {
        $existingInstanceId = $output
        Write-Info "Found instance: $existingInstanceId"
    }
}

if ($existingInstanceId) {
    Write-Info "Previous instance: $existingInstanceId"
    if (Read-YesNo "Terminate and launch fresh instance?" $true) {
        Write-Info "Terminating $existingInstanceId..."
        $ErrorActionPreference = "Continue"
        & aws ec2 terminate-instances --instance-ids $existingInstanceId --region $Region --profile $AwsProfile --output json 2>&1 | Out-Null
        $ErrorActionPreference = "Stop"
        Write-Ok "Instance termination initiated."
        $existingInstanceId = ""
    } else {
        Write-Info "Keeping existing instance. Exiting."
        exit 0
    }
} else {
    Write-Info "No previous instance to terminate."
}

Write-Step 7 11 "Run mode"
Write-Info "test - instance stopped after each test run (cheapest)"
Write-Info "demo - instance kept running + AD seeded with demo objects"

if (-not $Mode) {
    $modeInput = Read-WithDefault "Mode" "test"
    $Mode      = if ($modeInput.Trim() -eq "demo") { "demo" } else { "test" }
}

$KeepRunning  = ($Mode -eq "demo")
$SeedDemoData = ($Mode -eq "demo")
Write-Ok "Mode: $Mode (KeepRunning=$KeepRunning, SeedDemoData=$SeedDemoData)"

Write-Step 8 11 "IAM Role for SSM and RDP access"
Write-Info "Enables Systems Manager Session Manager and EC2 Instance Connect."
$IamRole = Get-OrCreateIamRole
$IamInstanceProfile = Get-OrCreateInstanceProfile $IamRole

Write-Step 9 11 "EC2 Key Pair for RDP access"
Write-Info "Used to RDP into the instance for troubleshooting."
$KeyPairName = "coreapi-test-dc-$Region"
$keyDir = Join-Path $RepoRoot ".ssh"
$keyPath = Join-Path $keyDir "$KeyPairName.pem"

if (Test-Path $keyPath) {
    Write-Ok "Key pair already exists locally: $keyPath"
} else {
    Write-Info "Creating new key pair: $KeyPairName"
    if (-not (Test-Path $keyDir)) { New-Item -ItemType Directory -Path $keyDir -Force | Out-Null }

    $ErrorActionPreference = "Continue"
    $raw = & aws ec2 create-key-pair --key-name $KeyPairName --region $Region --profile $AwsProfile --query 'KeyMaterial' --output text 2>&1
    $ErrorActionPreference = "Stop"
    if ($LASTEXITCODE -eq 0) {
        $raw | Set-Content $keyPath -Encoding utf8
        icacls $keyPath /inheritance:r /grant:r "$env:USERNAME`:F" | Out-Null
        Write-Ok "Key pair created and saved: $keyPath"
    } else {
        if ($raw -match "InvalidKeyPair.Duplicate") {
            Write-Warn "Key pair already exists in AWS. Using: $KeyPairName"
        } else {
            throw "Failed to create key pair: $raw"
        }
    }
}

Write-Step 10 11 "Provisioning AWS resources"

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

$config = [ordered]@{
    TestInfrastructure = [ordered]@{
        ProvisionAdDc           = $true
        AwsProfile              = $AwsProfile
        AwsRegion               = $Region
        InstanceType            = $InstanceType
        AmiId                   = $AmiId
        SecurityGroupId         = $SgId
        SubnetId                = ""
        KeyPairName             = $KeyPairName
        ElasticIpAllocationId   = $EipAllocationId
        ExistingInstanceId      = $existingInstanceId
        IamInstanceProfile      = $IamInstanceProfile
        DomainName              = $DomainName
        DomainNetbiosName       = $DomainNetbios
        AdAdminPassword         = $AdAdminPassword
        LdapReadyTimeoutSeconds = 1800
        KeepRunning             = $KeepRunning
        SeedDemoData            = $SeedDemoData
    }
}

$config | ConvertTo-Json -Depth 3 | Set-Content $ConfigPath -Encoding utf8
Write-Ok "Written: $ConfigPath"

Write-Banner "Setup complete"
$ErrorActionPreference = "Continue"
$account = & aws sts get-caller-identity --profile $AwsProfile --query Account --output text 2>&1
$ErrorActionPreference = "Stop"
Write-Host "  Profile       : $AwsProfile" -ForegroundColor White
Write-Host "  Account       : $account" -ForegroundColor White
Write-Host "  Region        : $Region" -ForegroundColor White
Write-Host "  AMI           : $AmiId" -ForegroundColor White
Write-Host "  Security Group: $SgId" -ForegroundColor White
Write-Host "  Key Pair      : $KeyPairName" -ForegroundColor White
Write-Host "  Key Path      : $keyPath" -ForegroundColor White
if ($EipAllocationId) { Write-Host "  Elastic IP    : $EipAllocationId" -ForegroundColor White }
Write-Host "  Mode          : $Mode" -ForegroundColor White
Write-Host ""
Write-Info "LDAP timeout: 30 min (allowing time for AD DS promotion + reboot)"
Write-Info "First launch: ~15-20 min (Windows boot + AD DS promotion + reboot)"
Write-Info "Later runs  : ~2-3 min (instance restart)"
Write-Info ""
Write-Info "To RDP into the instance once it's running:"
Write-Host "  mstsc /v:$Region-test-dc.your-domain.com (after IP is fixed by DNS)" -ForegroundColor White
Write-Host "  or use the Elastic IP from EC2 console" -ForegroundColor White
Write-Host ""

Write-Step 11 11 "Run integration tests"
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
