#Requires -Version 5.1
<#
.SYNOPSIS
    First-run wizard that provisions the AWS infrastructure required to run
    coreapi integration tests or a live demo against a real AD DS instance.

.DESCRIPTION
    What the wizard does, in order:
      1  Validates AWS CLI credentials
      2  Asks for region and instance type
      3  Asks for the AD domain name and administrator password
      4  Optionally allocates an Elastic IP so the DC always has the same address
      5  Finds the latest Windows Server 2022 Base AMI in the chosen region
      6  Creates (or reuses) a Security Group 'coreapi-test-dc' with inbound rules
         for LDAP (389), LDAPS (636), and RDP (3389) from your current public IP
      7  Asks for the run mode: test or demo
      8  Writes tests\CoreApi.IntegrationTests\appsettings.Development.json
      9  Optionally runs the integration tests immediately

    The script is idempotent: re-running it reuses existing AWS resources and
    preserves the ExistingInstanceId written after the first test run.

.PARAMETER Mode
    'test'  — instance is stopped after each test run (cheapest)
    'demo'  — instance stays running after tests, AD seeded with demo objects
    Omit to be asked interactively.

.EXAMPLE
    .\tools\setup-test-dc.ps1
    .\tools\setup-test-dc.ps1 -Mode demo
#>
[CmdletBinding()]
param(
    [ValidateSet("test", "demo", "")]
    [string]$Mode = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$RepoRoot    = Split-Path $PSScriptRoot -Parent
$ConfigPath  = Join-Path $RepoRoot "tests\CoreApi.IntegrationTests\appsettings.Development.json"

# ── Console helpers ──────────────────────────────────────────────────────────

function Write-Banner([string]$text) {
    Write-Host ""
    Write-Host ("=" * 56) -ForegroundColor Green
    Write-Host "  $text" -ForegroundColor Green
    Write-Host ("=" * 56) -ForegroundColor Green
}

function Write-Step([int]$n, [int]$total, [string]$text) {
    Write-Host ""
    Write-Host "Step $n/$total — $text" -ForegroundColor Cyan
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

function Write-Info([string]$text)    { Write-Host "  $text" -ForegroundColor DarkGray }
function Write-Ok([string]$text)      { Write-Host "  $text" -ForegroundColor Green }
function Write-Warn([string]$text)    { Write-Host "  $text" -ForegroundColor Yellow }

# ── AWS helpers ──────────────────────────────────────────────────────────────

function Assert-AwsCli {
    if (-not (Get-Command aws -ErrorAction SilentlyContinue)) {
        throw "AWS CLI not found. Install from https://aws.amazon.com/cli/ then run 'aws configure'."
    }
    $raw = aws sts get-caller-identity --output json
    if ($LASTEXITCODE -ne 0) {
        throw "AWS credentials not configured. Run 'aws configure' or export AWS_ACCESS_KEY_ID / AWS_SECRET_ACCESS_KEY."
    }
    $id = $raw | ConvertFrom-Json
    Write-Info "Authenticated as: $($id.Arn)"
}

function Get-MyPublicIp {
    try   { (Invoke-RestMethod "https://checkip.amazonaws.com").Trim() }
    catch { Read-Host "  Could not auto-detect your public IP. Enter it (without /32)" }
}

function Get-LatestWin2022Ami([string]$Region) {
    $amiId = aws ec2 describe-images --owners amazon `
        --filters "Name=name,Values=Windows_Server-2022-English-Full-Base-*" `
                  "Name=state,Values=available" `
        --query "sort_by(Images, &CreationDate)[-1].ImageId" `
        --region $Region --output text
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($amiId) -or $amiId -eq "None") {
        throw "No Windows Server 2022 AMI found in region '$Region'."
    }
    $amiId.Trim()
}

function Get-OrCreateSecurityGroup([string]$Region) {
    $name     = "coreapi-test-dc"
    $existing = aws ec2 describe-security-groups `
        --filters "Name=group-name,Values=$name" `
        --query "SecurityGroups[0].GroupId" `
        --region $Region --output text
    if ($LASTEXITCODE -eq 0 -and $existing -and $existing -ne "None") {
        Write-Info "Reusing Security Group: $($existing.Trim())"
        return $existing.Trim()
    }
    Write-Info "Creating Security Group '$name'..."
    $raw  = aws ec2 create-security-group `
        --group-name $name `
        --description "CoreAPI test/demo DC — LDAP + LDAPS + RDP" `
        --region $Region --output json
    ($raw | ConvertFrom-Json).GroupId
}

function Add-InboundRule([string]$SgId, [int]$Port, [string]$Cidr, [string]$Region) {
    # Non-zero exit on duplicate rule is harmless — ignore it
    $prev = $ErrorActionPreference
    $ErrorActionPreference = "Continue"
    aws ec2 authorize-security-group-ingress `
        --group-id $SgId --protocol tcp --port $Port --cidr $Cidr `
        --region $Region --output json | Out-Null
    $ErrorActionPreference = $prev
}

function New-ElasticIp([string]$Region) {
    $raw = aws ec2 allocate-address --domain vpc --region $Region --output json
    if ($LASTEXITCODE -ne 0) { throw "Failed to allocate Elastic IP." }
    $raw | ConvertFrom-Json
}

# ── Read existing config (for re-runs) ──────────────────────────────────────

$existingInstanceId = ""
$existingEipAllocId = ""
$isUpdate           = $false

if (Test-Path $ConfigPath) {
    try {
        $prev = Get-Content $ConfigPath -Raw | ConvertFrom-Json
        $ti   = $prev.TestInfrastructure
        $existingInstanceId = if ($ti.ExistingInstanceId) { $ti.ExistingInstanceId } else { "" }
        $existingEipAllocId = if ($ti.ElasticIpAllocationId) { $ti.ElasticIpAllocationId } else { "" }
        $isUpdate = $true
    } catch { }
}

# ── Banner ───────────────────────────────────────────────────────────────────

Write-Banner "CoreAPI — EC2 test/demo DC setup wizard"
Write-Info "Output : $ConfigPath"
if ($existingInstanceId) {
    Write-Info "Existing instance detected: $existingInstanceId (will be preserved)"
}
if ($isUpdate) {
    Write-Warn "Config file already exists. Answers below will overwrite it."
    if (-not (Read-YesNo "Continue?" $true)) { Write-Host "Aborted."; exit 0 }
}

# ── Step 1 — AWS credentials ─────────────────────────────────────────────────

Write-Step 1 7 "AWS credentials"
Assert-AwsCli

# ── Step 2 — Region & instance type ─────────────────────────────────────────

Write-Step 2 7 "Region & instance type"
$defaultRegion = if ($isUpdate -and $prev.TestInfrastructure.AwsRegion) { $prev.TestInfrastructure.AwsRegion } else { "us-east-1" }
$defaultType   = if ($isUpdate -and $prev.TestInfrastructure.InstanceType) { $prev.TestInfrastructure.InstanceType } else { "t3.medium" }

$Region       = Read-WithDefault "AWS region" $defaultRegion
$InstanceType = Read-WithDefault "Instance type (t3.medium ~`$0.075/hr)" $defaultType

# ── Step 3 — AD domain ───────────────────────────────────────────────────────

Write-Step 3 7 "Active Directory domain"
$defaultDomain  = if ($isUpdate -and $prev.TestInfrastructure.DomainName) { $prev.TestInfrastructure.DomainName } else { "corp.local" }
$defaultNetbios = if ($isUpdate -and $prev.TestInfrastructure.DomainNetbiosName) { $prev.TestInfrastructure.DomainNetbiosName } else { "CORP" }

$DomainName    = Read-WithDefault "Domain FQDN" $defaultDomain
$DomainNetbios = Read-WithDefault "NETBIOS name" $defaultNetbios

# ── Step 4 — Password ────────────────────────────────────────────────────────

Write-Step 4 7 "AD Administrator password"
Write-Info "Min 8 chars with uppercase, lowercase, digit and symbol."
Write-Info "Written to the local config file only (gitignored — never committed)."

$existingPwd     = if ($isUpdate -and $prev.TestInfrastructure.AdAdminPassword) { $prev.TestInfrastructure.AdAdminPassword } else { "" }
$AdAdminPassword = if ($existingPwd -and (Read-YesNo "Keep existing password?" $true)) {
    $existingPwd
} else {
    $p1 = Read-SecureString "Password"
    $p2 = Read-SecureString "Confirm password"
    if ($p1 -ne $p2) { throw "Passwords do not match." }
    $p1
}

# ── Step 5 — Elastic IP ──────────────────────────────────────────────────────

Write-Step 5 7 "Elastic IP"
Write-Info "An Elastic IP keeps the DC at the same address across start/stop cycles."
Write-Info "Without one you must update ExistingInstanceId after each start."

$EipAllocationId = $existingEipAllocId
if ($existingEipAllocId) {
    Write-Info "Existing allocation preserved: $existingEipAllocId"
} elseif (Read-YesNo "Allocate an Elastic IP?" $true) {
    Write-Info "Allocating..."
    $eip             = New-ElasticIp $Region
    $EipAllocationId = $eip.AllocationId
    Write-Ok "Allocated: $($eip.PublicIp) ($EipAllocationId)"
}

# ── Step 6 — Mode ────────────────────────────────────────────────────────────

Write-Step 6 7 "Run mode"
Write-Info "test  — instance stopped after each test run (cheapest)"
Write-Info "demo  — instance kept running + demo AD objects seeded (Users / Groups / SvcAccounts)"

if (-not $Mode) {
    $modeInput = Read-WithDefault "Mode" "test"
    $Mode      = if ($modeInput.Trim() -eq "demo") { "demo" } else { "test" }
}

$KeepRunning  = ($Mode -eq "demo")
$SeedDemoData = ($Mode -eq "demo")
Write-Ok "Mode: $Mode (KeepRunning=$KeepRunning, SeedDemoData=$SeedDemoData)"

# ── Step 7 — AWS resources ───────────────────────────────────────────────────

Write-Step 7 7 "Provisioning AWS resources"

Write-Info "Finding latest Windows Server 2022 Base AMI in $Region..."
$AmiId = Get-LatestWin2022Ami $Region
Write-Ok "AMI: $AmiId"

$SgId = Get-OrCreateSecurityGroup $Region
Write-Ok "Security Group: $SgId"

Write-Info "Detecting your public IP..."
$myIp   = Get-MyPublicIp
$myCidr = "$myIp/32"
Write-Info "Adding inbound rules for $myCidr..."
Add-InboundRule $SgId 389  $myCidr $Region   # LDAP
Add-InboundRule $SgId 636  $myCidr $Region   # LDAPS
Add-InboundRule $SgId 3389 $myCidr $Region   # RDP (troubleshooting)
Write-Ok "Inbound: TCP 389 (LDAP), 636 (LDAPS), 3389 (RDP) from $myCidr"

# ── Write config ─────────────────────────────────────────────────────────────

$config = [ordered]@{
    TestInfrastructure = [ordered]@{
        ProvisionAdDc           = $true
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

# ── Summary ──────────────────────────────────────────────────────────────────

Write-Banner "Setup complete"
Write-Host "  Mode          : $Mode" -ForegroundColor White
Write-Host "  Region        : $Region" -ForegroundColor White
Write-Host "  AMI           : $AmiId" -ForegroundColor White
Write-Host "  Security Group: $SgId" -ForegroundColor White
if ($EipAllocationId) {
    Write-Host "  Elastic IP    : $EipAllocationId" -ForegroundColor White
}
Write-Host ""
Write-Info "First run: ~12-15 min (Windows boot + AD DS promotion + reboot)."
Write-Info "Later runs reuse the stopped instance: ~2-3 min."
if ($Mode -eq "demo") {
    Write-Info "After tests, the DC stays running. Point coreapi at the IP printed in test output."
}
Write-Host ""

if (Read-YesNo "Run integration tests now?" $true) {
    Push-Location $RepoRoot
    try {
        dotnet test tests\CoreApi.IntegrationTests --filter "Category=Integration" -v normal
        if ($LASTEXITCODE -eq 0) {
            Write-Ok "All integration tests passed."
        } else {
            Write-Warn "Some tests failed — check output above."
        }
    } finally {
        Pop-Location
    }
} else {
    Write-Host ""
    Write-Info "When ready:"
    Write-Host "  dotnet test tests\CoreApi.IntegrationTests --filter `"Category=Integration`" -v normal" -ForegroundColor White
}
