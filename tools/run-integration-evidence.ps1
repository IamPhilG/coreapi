#requires -Version 5.1
<#
.SYNOPSIS
    Runs the CoreApi AD/AWS integration tests and captures a durable, tamper-evident evidence bundle.

.DESCRIPTION
    Produces artifacts/integration/<timestamp>/ containing a transcript, a TRX result file, the full
    dotnet-test console output, and a JSON manifest of non-sensitive campaign provenance plus a
    SHA-256 fingerprint of each captured artifact.

    Secrets are NEVER read, displayed, or written: this script does not touch the AD administrator
    credential or any AWS credential. Only the test outcome and non-sensitive context are recorded.

    Provenance gate:
      * An official campaign refuses to start on a dirty working tree (uncommitted changes).
      * -AllowDirtyWorkingTree runs anyway, but marks the bundle evidenceStatus=development-only and
        records the porcelain status -- it can never be presented as complete official evidence.

    Completeness (official mode): when tests pass but AWS identity, the instance, the required
    final instance state, or mandatory metadata cannot be collected, the bundle is marked
    evidenceStatus=incomplete and the script returns 2 (distinct from a test failure).

    Exit-code rule:
      * tests failed                              -> the tests' exit code
      * tests passed, evidence complete/dev-only  -> 0
      * tests passed, evidence incomplete          -> 2 (0 only with -AllowIncompleteEvidence)

    Run from anywhere in the repo. It uses the existing gitignored configuration
    (tests/**/appsettings.Development.json) and standard AWS/LDAP environment variables.
#>
[CmdletBinding()]
param(
    [string]$TestProject = 'tests/CoreApi.IntegrationTests/CoreApi.IntegrationTests.csproj',
    [string]$Configuration = 'Release',
    [switch]$AllowDirtyWorkingTree,
    [switch]$AllowIncompleteEvidence,
    # Self-test hooks (local only): skip the real dotnet test run and inject a result so the manifest
    # and exit-code logic can be exercised with no AWS/AD and no dotnet. Never yields 'complete'.
    [switch]$SkipTestExecution,
    [int]$SimulateTestExitCode = 0
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
# Native non-zero exit codes must set $LASTEXITCODE, not throw (PS 7.3+ default would otherwise
# turn a failing dotnet test into a terminating error and lose the exit code).
$PSNativeCommandUseErrorActionPreference = $false

$EvidenceVersion = '1.0.0'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$scriptExitCode = 1
$transcriptStarted = $false

function Get-SafeOutput {
    param([string]$Exe, [string[]]$Arguments)
    try {
        $value = (& $Exe @Arguments 2>$null | Out-String).Trim()
        if ([string]::IsNullOrWhiteSpace($value)) { return $null }
        return $value
    }
    catch { return $null }
}

function Get-Sha256Tag {
    param([string]$Path)
    if (Test-Path $Path) {
        return "sha256:$((Get-FileHash -Path $Path -Algorithm SHA256).Hash.ToLowerInvariant())"
    }
    return $null
}

# --- Provenance gate (before any artifact is produced) ---
$porcelain = (& git -C $repoRoot status --porcelain 2>$null | Out-String).TrimEnd()
$workingTreeClean = [string]::IsNullOrWhiteSpace($porcelain)

if (-not $workingTreeClean -and -not $AllowDirtyWorkingTree) {
    [Console]::Error.WriteLine(
        'Refusing to start an official evidence campaign: the working tree has uncommitted changes. ' +
        'Commit or stash them, or re-run with -AllowDirtyWorkingTree for a development-only bundle.')
    exit 3
}

$developmentOnly = -not $workingTreeClean

Push-Location $repoRoot
try {
    $timestamp = (Get-Date).ToUniversalTime().ToString('yyyyMMddTHHmmssZ')
    $timestampStartedUtc = (Get-Date).ToUniversalTime().ToString('o')
    $evidenceDir = Join-Path $repoRoot "artifacts/integration/$timestamp"
    New-Item -ItemType Directory -Path $evidenceDir -Force | Out-Null

    $transcriptPath = Join-Path $evidenceDir 'transcript.log'
    $outputPath = Join-Path $evidenceDir 'dotnet-test-output.log'
    $trxPath = Join-Path $evidenceDir 'integration-tests.trx'
    $manifestPath = Join-Path $evidenceDir 'manifest.json'

    Start-Transcript -Path $transcriptPath -Force | Out-Null
    $transcriptStarted = $true
    Write-Host "Evidence directory: $evidenceDir"
    if ($developmentOnly) { Write-Host 'WARNING: development-only bundle (dirty working tree).' }

    # --- Run the integration tests (or the self-test stub) ---
    if ($SkipTestExecution) {
        Write-Host "SkipTestExecution: not running dotnet; injecting test exit code $SimulateTestExitCode."
        Set-Content -Path $trxPath -Value '<TestRun placeholder="self-test" />' -Encoding utf8
        Set-Content -Path $outputPath -Value 'self-test: dotnet test skipped' -Encoding utf8
        $testExitCode = $SimulateTestExitCode
    }
    else {
        & dotnet test $TestProject `
            --configuration $Configuration `
            --logger 'trx;LogFileName=integration-tests.trx' `
            --results-directory $evidenceDir 2>&1 |
            Tee-Object -FilePath $outputPath
        $testExitCode = $LASTEXITCODE
    }

    $timestampCompletedUtc = (Get-Date).ToUniversalTime().ToString('o')

    # --- Non-secret campaign metadata (best effort; missing values stay $null) ---
    $awsAccount = Get-SafeOutput 'aws' @('sts', 'get-caller-identity', '--query', 'Account', '--output', 'text')
    $awsPrincipalArn = Get-SafeOutput 'aws' @('sts', 'get-caller-identity', '--query', 'Arn', '--output', 'text')

    $region = $env:TESTINFRA__AwsRegion
    if ([string]::IsNullOrWhiteSpace($region)) { $region = $env:AWS_REGION }
    if ([string]::IsNullOrWhiteSpace($region)) { $region = $null }

    $instanceId = $env:TESTINFRA__ExistingInstanceId
    if ([string]::IsNullOrWhiteSpace($instanceId)) { $instanceId = $null }

    $keepRunning = ($env:TESTINFRA__KeepRunning -eq 'true')

    # Official mode: when the DC is meant to stop, wait for and require the terminal 'stopped'
    # state -- 'stopping' is not accepted.
    $instanceState = $null
    if ($instanceId -and -not $keepRunning) {
        Get-SafeOutput 'aws' @('ec2', 'wait', 'instance-stopped', '--instance-ids', $instanceId) | Out-Null
    }
    if ($instanceId) {
        $instanceState = Get-SafeOutput 'aws' @('ec2', 'describe-instances', '--instance-ids', $instanceId,
            '--query', 'Reservations[0].Instances[0].State.Name', '--output', 'text')
    }

    $protocol = $null
    if ($env:LDAP__UseTls -eq 'true') { $protocol = 'LDAPS' }
    elseif ($env:LDAP__UseTls -eq 'false') { $protocol = 'LDAP' }

    $port = $null
    if (-not [string]::IsNullOrWhiteSpace($env:LDAP__Port)) { $port = [int]$env:LDAP__Port }

    # --- Completeness + evidence status ---
    $metadataComplete = ($null -ne $awsAccount) -and ($null -ne $awsPrincipalArn) -and
        ($null -ne $instanceId) -and ($null -ne $region) -and ($null -ne $protocol) -and ($null -ne $port)
    $finalStateOk = $keepRunning -or ($instanceState -eq 'stopped')
    $evidenceComplete = $metadataComplete -and $finalStateOk

    if ($developmentOnly) { $evidenceStatus = 'development-only' }
    elseif ($evidenceComplete) { $evidenceStatus = 'complete' }
    else { $evidenceStatus = 'incomplete' }

    # A self-test bundle can never be presented as complete.
    if ($SkipTestExecution -and $evidenceStatus -eq 'complete') { $evidenceStatus = 'incomplete' }

    # Finalize the transcript before hashing so the recorded hash matches the closed file.
    if ($transcriptStarted) { Stop-Transcript | Out-Null; $transcriptStarted = $false }

    $artifactHashes = [ordered]@{
        'integration-tests.trx'  = Get-Sha256Tag $trxPath
        'transcript.log'         = Get-Sha256Tag $transcriptPath
        'dotnet-test-output.log' = Get-Sha256Tag $outputPath
    }

    $manifest = [ordered]@{
        evidenceVersion       = $EvidenceVersion
        evidenceStatus        = $evidenceStatus
        commitSha             = Get-SafeOutput 'git' @('-C', $repoRoot, 'rev-parse', 'HEAD')
        branch                = Get-SafeOutput 'git' @('-C', $repoRoot, 'rev-parse', '--abbrev-ref', 'HEAD')
        workingTreeClean      = $workingTreeClean
        gitStatusPorcelain    = $porcelain
        timestampStartedUtc   = $timestampStartedUtc
        timestampCompletedUtc = $timestampCompletedUtc
        dotnetVersion         = Get-SafeOutput 'dotnet' @('--version')
        awsAccount            = $awsAccount
        awsPrincipalArn       = $awsPrincipalArn
        awsRegion             = $region
        instanceId            = $instanceId
        ldapProtocol          = $protocol
        ldapPort              = $port
        testResult            = if ($testExitCode -eq 0) { 'passed' } else { 'failed' }
        testExitCode          = $testExitCode
        finalInstanceState    = $instanceState
        artifactHashes        = $artifactHashes
    }

    $manifest | ConvertTo-Json -Depth 6 | Out-File -FilePath $manifestPath -Encoding utf8
    Write-Host "Manifest written: $manifestPath (evidenceStatus=$evidenceStatus)"

    # --- Exit-code rule ---
    if ($testExitCode -ne 0) {
        $scriptExitCode = $testExitCode
    }
    elseif ($evidenceStatus -eq 'complete' -or $evidenceStatus -eq 'development-only') {
        $scriptExitCode = 0
    }
    elseif ($AllowIncompleteEvidence) {
        $scriptExitCode = 0
    }
    else {
        $scriptExitCode = 2
    }
}
finally {
    if ($transcriptStarted) { try { Stop-Transcript | Out-Null } catch { } }
    Pop-Location
}

exit $scriptExitCode
