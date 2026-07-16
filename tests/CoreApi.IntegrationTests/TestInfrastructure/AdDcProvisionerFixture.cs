using System.Diagnostics;
using System.DirectoryServices.Protocols;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace CoreApi.IntegrationTests.TestInfrastructure;

/// <summary>
/// xUnit collection fixture that optionally provisions an EC2 Windows Server AD DS instance
/// before integration tests run.
///
/// Modes
/// -----
/// Test mode  (KeepRunning=false, SeedDemoData=false) -- default
///   Starts the DC, runs tests, stops the instance on teardown.
///
/// Demo mode  (KeepRunning=true, SeedDemoData=true)
///   Starts the DC, seeds demo AD objects (OUs, users, groups, service account),
///   runs tests, then LEAVES the instance running so the coreapi app can be pointed
///   at it for a live demo.
///
/// When ProvisionAdDc=false (the default), no AWS calls are made and connection
/// parameters fall through to LDAP__* environment variables.
///
/// Configuration sources (env vars take precedence over the file):
///   File : tests/CoreApi.IntegrationTests/appsettings.Development.json  (gitignored)
///   Env  : TESTINFRA__* (e.g. TESTINFRA__ProvisionAdDc=true)
/// </summary>
public sealed class AdDcProvisionerFixture : IAsyncLifetime
{
    public string ResolvedHost { get; private set; } =
        Environment.GetEnvironmentVariable("LDAP__Host") ?? "localhost";

    public string ResolvedBaseDn { get; private set; } =
        Environment.GetEnvironmentVariable("LDAP__BaseDn") ?? "DC=corp,DC=local";

    public int ResolvedPort { get; private set; } =
        int.TryParse(Environment.GetEnvironmentVariable("LDAP__Port"), out var p) ? p : 389;

    public bool ResolvedUseTls { get; private set; } =
        bool.TryParse(Environment.GetEnvironmentVariable("LDAP__UseTls"), out var tls) && tls;

    public string ResolvedServiceAccountUser { get; private set; } =
        Environment.GetEnvironmentVariable("LDAP__ServiceAccountUser") ?? string.Empty;

    public string ResolvedServiceAccountPassword { get; private set; } =
        Environment.GetEnvironmentVariable("LDAP__ServiceAccountPassword") ?? string.Empty;

    private AdDcProvisionerOptions _options = new();
    private string? _launchedInstanceId;

    public async Task InitializeAsync()
    {
        _options = LoadOptions();

        if (!_options.ProvisionAdDc)
        {
            WarnIfNoLdapTarget();
            return;
        }

        ValidateRequiredOptions(_options);

        string instanceId;
        if (!string.IsNullOrEmpty(_options.ExistingInstanceId))
        {
            instanceId = _options.ExistingInstanceId;
            Console.WriteLine($"[AdDcProvisioner] Checking existing instance state: {instanceId}");
            string state = await GetInstanceStateAsync(instanceId);
            Console.WriteLine($"[AdDcProvisioner] Instance state: {state}");

            if (state == "running")
            {
                Console.WriteLine($"[AdDcProvisioner] ✓ Instance is already running");
            }
            else if (state == "stopped")
            {
                Console.WriteLine($"[AdDcProvisioner] Restarting stopped instance...");
                await StartExistingInstanceAsync(instanceId);
                Console.WriteLine($"[AdDcProvisioner] ✓ Instance restarted");
            }
            else
            {
                Console.WriteLine($"[AdDcProvisioner] Instance in invalid state '{state}'. Terminating and launching new instance...");
                await TerminateInstanceAsync(instanceId);
                Console.WriteLine($"[AdDcProvisioner] Launching new EC2 instance...");
                instanceId = await LaunchNewInstanceAsync();
                _launchedInstanceId = instanceId;
                Console.WriteLine($"[AdDcProvisioner] ✓ Instance launched: {instanceId}");
            }
        }
        else
        {
            Console.WriteLine("[AdDcProvisioner] Launching new EC2 instance...");
            instanceId = await LaunchNewInstanceAsync();
            _launchedInstanceId = instanceId;
            Console.WriteLine($"[AdDcProvisioner] ✓ Instance launched: {instanceId}");
            Console.WriteLine(
                $"[AdDcProvisioner] Add as TestInfrastructure:ExistingInstanceId to reuse on subsequent runs.");
        }

        Console.WriteLine("[AdDcProvisioner] Acquiring public IP...");
        string publicIp = await WaitForPublicIpAsync(instanceId);
        Console.WriteLine($"[AdDcProvisioner] ✓ Public IP: {publicIp}");

        if (!string.IsNullOrEmpty(_options.ElasticIpAllocationId))
        {
            Console.WriteLine($"[AdDcProvisioner] Associating Elastic IP (may retry if instance not ready)...");
            await AssociateElasticIpWithRetryAsync(instanceId, _options.ElasticIpAllocationId);
            Console.WriteLine($"[AdDcProvisioner] ✓ Elastic IP associated");
            publicIp = await WaitForPublicIpAsync(instanceId);
            Console.WriteLine($"[AdDcProvisioner] ✓ IP confirmed: {publicIp}");
        }

        Console.WriteLine($"[AdDcProvisioner] Waiting for LDAP (AD DS promotion)... (timeout: {_options.LdapReadyTimeoutSeconds}s)");
        await WaitForLdapAsync(publicIp);
        Console.WriteLine($"[AdDcProvisioner] ✓ LDAP port 389 responding - AD DS ready");

        Console.WriteLine("[AdDcProvisioner] Verifying AD Administrator bind credentials...");
        await VerifyAdministratorCredentialsAsync(publicIp);
        Console.WriteLine("[AdDcProvisioner] ✓ AD Administrator bind credentials verified");

        ResolvedHost = publicIp;
        ResolvedBaseDn = DomainNameToBaseDn(_options.DomainName);
        ResolvedPort = 389;
        ResolvedUseTls = false;
        ResolvedServiceAccountUser = $"Administrator@{_options.DomainName}";
        ResolvedServiceAccountPassword = _options.AdAdminPassword;

        if (_options.SeedDemoData)
        {
            Console.WriteLine("[AdDcProvisioner] Seeding demo AD data...");
            await Task.Run(SeedDemoData);
            Console.WriteLine("[AdDcProvisioner] ✓ Demo data seeded");
        }

        Console.WriteLine("[AdDcProvisioner] ✓ Initialization complete - ready for tests");
    }

    public async Task DisposeAsync()
    {
        if (!_options.ProvisionAdDc)
            return;

        if (_options.KeepRunning)
        {
            Console.WriteLine(
                $"[AdDcProvisioner] KeepRunning=true -- instance left running. " +
                $"Point coreapi at {ResolvedHost} (BaseDn: {ResolvedBaseDn}).");
            return;
        }

        string? toStop = _launchedInstanceId
            ?? (_options.ProvisionAdDc && !string.IsNullOrEmpty(_options.ExistingInstanceId)
                ? _options.ExistingInstanceId
                : null);

        if (toStop is not null)
        {
            await StopInstanceAsync(toStop);
        }
    }

    // -- EC2 helpers via AWS CLI --

    private async Task InvokeAwsAsync(params string[] args)
    {
        var profile = string.IsNullOrEmpty(_options.AwsProfile) ? "" : $" --profile {_options.AwsProfile}";
        var cmdArgs = string.Join(" ", args.Select(a => a.Contains(" ") ? $"\"{a}\"" : a)) + profile;
        var psi = new ProcessStartInfo("aws", cmdArgs)
        {
            RedirectStandardError = true,
            UseShellExecute       = false
        };
        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start aws CLI.");
        await proc.WaitForExitAsync();
        if (proc.ExitCode != 0)
        {
            string err = proc.StandardError.ReadToEnd();
            throw new InvalidOperationException($"AWS CLI error: {err}");
        }
    }

    private async Task<string> QueryAwsAsync(string query, params string[] args)
    {
        var profile = string.IsNullOrEmpty(_options.AwsProfile) ? "" : $" --profile {_options.AwsProfile}";
        var cmdArgs = string.Join(" ", args.Select(a => a.Contains(" ") ? $"\"{a}\"" : a)) +
                      $" --query \"{query}\" --output text" + profile;
        var psi = new ProcessStartInfo("aws", cmdArgs)
        {
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute        = false
        };
        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start aws CLI.");
        string stdout = proc.StandardOutput.ReadToEnd();
        await proc.WaitForExitAsync();
        if (proc.ExitCode != 0)
        {
            string err = proc.StandardError.ReadToEnd();
            throw new InvalidOperationException($"AWS CLI error: {err}");
        }
        return stdout.Trim();
    }

    private async Task StartExistingInstanceAsync(string instanceId)
    {
        await InvokeAwsAsync("ec2", "start-instances", "--instance-ids", instanceId,
            "--region", _options.AwsRegion);
        await WaitForInstanceStateAsync(instanceId, "running");
    }

    private async Task<string> GetInstanceStateAsync(string instanceId)
    {
        try
        {
            return await QueryAwsAsync(
                "Reservations[0].Instances[0].State.Name",
                "ec2", "describe-instances", "--instance-ids", instanceId,
                "--region", _options.AwsRegion);
        }
        catch
        {
            return "unknown";
        }
    }

    private async Task TerminateInstanceAsync(string instanceId)
    {
        try
        {
            Console.WriteLine($"[AdDcProvisioner] Terminating instance {instanceId}...");
            await InvokeAwsAsync("ec2", "terminate-instances", "--instance-ids", instanceId,
                "--region", _options.AwsRegion);
            // Wait up to 2 minutes for termination to complete
            DateTime start = DateTime.UtcNow;
            while (DateTime.UtcNow - start < TimeSpan.FromMinutes(2))
            {
                string state = await GetInstanceStateAsync(instanceId);
                if (state == "terminated")
                {
                    Console.WriteLine($"[AdDcProvisioner] ✓ Instance terminated");
                    return;
                }
                await Task.Delay(5000);
            }
            Console.WriteLine($"[AdDcProvisioner] Termination timeout - instance may still be terminating");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdDcProvisioner] Error terminating instance: {ex.Message}");
        }
    }

    private async Task<string> LaunchNewInstanceAsync()
    {
        if (string.IsNullOrEmpty(_options.AmiId))
            throw new InvalidOperationException(
                "TestInfrastructure:AmiId is required when ExistingInstanceId is not set.");
        if (string.IsNullOrEmpty(_options.AdAdminPassword))
            throw new InvalidOperationException(
                "TestInfrastructure:AdAdminPassword is required.");
        if (string.IsNullOrEmpty(_options.IamInstanceProfile))
            throw new InvalidOperationException(
                "TestInfrastructure:IamInstanceProfile is required because AD DS promotion is executed via SSM RunCommand. " +
                "Run 'tools\\setup-test-dc.ps1' to create the IAM role and instance profile.");

        // Check if an instance with the Elastic IP is already running (idempotent).
        if (!string.IsNullOrEmpty(_options.ElasticIpAllocationId))
        {
            string? existingId = await TryGetInstanceIdForElasticIpAsync(_options.ElasticIpAllocationId);
            if (existingId is not null)
            {
                Console.WriteLine(
                    $"[AdDcProvisioner] Instance {existingId} already has Elastic IP {_options.ElasticIpAllocationId}. Checking readiness.");
                string state = await GetInstanceStateAsync(existingId);
                if (state == "stopped")
                {
                    await StartExistingInstanceAsync(existingId);
                }
                else if (state != "running")
                {
                    throw new InvalidOperationException(
                        $"Instance {existingId} has Elastic IP {_options.ElasticIpAllocationId} but is in state '{state}'. " +
                        "Terminate it or wait for it to become stable before rerunning integration tests.");
                }

                string publicIp = await WaitForPublicIpAsync(existingId);
                if (await CanConnectTcpAsync(publicIp, 389, TimeSpan.FromSeconds(5)))
                {
                    Console.WriteLine($"[AdDcProvisioner] ✓ Existing instance already responds on LDAP port 389. Reusing.");
                    return existingId;
                }

                Console.WriteLine($"[AdDcProvisioner] Existing instance does not respond on LDAP port 389; running AD DS promotion via SSM.");
                await AssertIamInstanceProfileAttachedAsync(existingId);
                await WaitForSsmOnlineAsync(existingId);
                await ExecuteUserDataViaSSMAsync(existingId);
                return existingId;
            }
        }

        // Don't use --user-data (EC2Launch v2 doesn't decode base64 properly)
        // Instead, launch bare instance and execute script via SSM RunCommand after
        var args = new List<string>
        {
            "ec2", "run-instances",
            "--image-id", _options.AmiId,
            "--instance-type", _options.InstanceType,
            "--security-group-ids", _options.SecurityGroupId,
            "--tag-specifications", "ResourceType=instance,Tags=[{Key=Name,Value=coreapi-test-dc},{Key=coreapi-managed,Value=true}]",
            "--region", _options.AwsRegion
        };

        if (!string.IsNullOrEmpty(_options.SubnetId))
        {
            args.Add("--subnet-id");
            args.Add(_options.SubnetId);
        }

        if (!string.IsNullOrEmpty(_options.KeyPairName))
        {
            args.Add("--key-name");
            args.Add(_options.KeyPairName);
        }

        if (!string.IsNullOrEmpty(_options.IamInstanceProfile))
        {
            args.Add("--iam-instance-profile");
            args.Add($"Name={_options.IamInstanceProfile}");
        }

        string instanceId = await QueryAwsAsync("Instances[0].InstanceId", args.ToArray());

        await WaitForInstanceStateAsync(instanceId, "running");
        await AssertIamInstanceProfileAttachedAsync(instanceId);
        await WaitForSsmOnlineAsync(instanceId);

        // Execute AD DS promotion script via SSM RunCommand (bypasses EC2Launch v2 base64 issue)
        Console.WriteLine($"[AdDcProvisioner] Executing AD DS promotion via SSM RunCommand...");
        await ExecuteUserDataViaSSMAsync(instanceId);
        Console.WriteLine($"[AdDcProvisioner] ✓ AD DS promotion script executed");

        return instanceId;
    }

    private async Task<string?> TryGetInstanceIdForElasticIpAsync(string allocationId)
    {
        try
        {
            string instanceId = await QueryAwsAsync(
                "Addresses[0].InstanceId",
                "ec2", "describe-addresses",
                "--allocation-ids", allocationId,
                "--region", _options.AwsRegion);
            return string.IsNullOrEmpty(instanceId) || instanceId == "None" ? null : instanceId;
        }
        catch
        {
            return null;
        }
    }

    private async Task AssertIamInstanceProfileAttachedAsync(string instanceId)
    {
        string arn = await QueryAwsAsync(
            "Reservations[0].Instances[0].IamInstanceProfile.Arn",
            "ec2", "describe-instances", "--instance-ids", instanceId,
            "--region", _options.AwsRegion);

        if (string.IsNullOrEmpty(arn) || arn == "None")
            throw new InvalidOperationException(
                $"Instance {instanceId} does not have an IAM instance profile attached. " +
                $"Expected '{_options.IamInstanceProfile}' so the SSM agent can register. " +
                "Terminate the instance and re-run the setup after fixing the launch arguments.");

        Console.WriteLine($"[AdDcProvisioner] ✓ IAM instance profile attached: {arn}");
    }

    private async Task WaitForSsmOnlineAsync(string instanceId)
    {
        Console.WriteLine($"[AdDcProvisioner] Waiting for SSM managed instance to come online: {instanceId}...");
        DateTime startTime = DateTime.UtcNow;
        DateTime lastLogTime = startTime;

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(15));
        while (!cts.Token.IsCancellationRequested)
        {
            string pingStatus = await QueryAwsAsync(
                $"InstanceInformationList[?InstanceId=='{instanceId}'].PingStatus | [0]",
                "ssm", "describe-instance-information",
                "--region", _options.AwsRegion);

            if (pingStatus == "Online")
            {
                Console.WriteLine($"[AdDcProvisioner] ✓ SSM managed instance online");
                return;
            }

            if (DateTime.UtcNow - lastLogTime > TimeSpan.FromSeconds(30))
            {
                int elapsed = (int)(DateTime.UtcNow - startTime).TotalSeconds;
                string status = string.IsNullOrEmpty(pingStatus) || pingStatus == "None"
                    ? "not registered"
                    : pingStatus;
                Console.WriteLine($"[AdDcProvisioner] Still waiting for SSM... (status: {status}, {elapsed}s elapsed)");
                lastLogTime = DateTime.UtcNow;
            }

            try { await Task.Delay(TimeSpan.FromSeconds(10), cts.Token); }
            catch (OperationCanceledException)
            {
                throw new TimeoutException(
                    $"Instance {instanceId} did not become an SSM managed instance within 15 minutes. " +
                    "Check that the IAM instance profile has AmazonSSMManagedInstanceCore and that the instance can reach SSM endpoints.");
            }
        }
    }

    private async Task ExecuteUserDataViaSSMAsync(string instanceId)
    {
        // Use AWS Systems Manager Run Command to execute the PowerShell script
        // This bypasses EC2Launch v2 base64 decoding issues
        string script = BuildUserDataScript().Replace("<powershell>", "").Replace("</powershell>", "").Trim();
        await ExecutePowerShellViaSSMAsync(instanceId, script, "AD DS promotion");
    }

    private async Task ExecutePowerShellViaSSMAsync(string instanceId, string script, string operationName)
    {
        string payloadPath = WriteSsmCommandPayload(instanceId, script);

        // Send SSM command with timeout
        Console.WriteLine($"[AdDcProvisioner] Sending SSM RunCommand for {operationName}...");
        string? commandId = null;
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            string commandOutput = await QueryAwsAsync(
                "Command.CommandId",
                "ssm", "send-command",
                "--cli-input-json", $"file://{payloadPath}",
                "--region", _options.AwsRegion,
                "--output", "json");

            commandId = commandOutput.Trim();
            Console.WriteLine($"[AdDcProvisioner] ✓ SSM Command sent, ID: {commandId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AdDcProvisioner] ✗ Failed to send SSM command: {ex.Message}");
            throw;
        }
        finally
        {
            TryDeleteTempFile(payloadPath);
        }

        if (string.IsNullOrEmpty(commandId))
            throw new InvalidOperationException("SSM command ID is empty");

        // Wait for command to complete (timeout 30 min)
        Console.WriteLine($"[AdDcProvisioner] Polling SSM command status (30 min timeout)...");
        DateTime start = DateTime.UtcNow;
        DateTime lastLogTime = start;
        while (DateTime.UtcNow - start < TimeSpan.FromMinutes(30))
        {
            // Check invocation status
            string? status = null;
            try
            {
                status = await QueryAwsAsync(
                    "CommandInvocations[0].Status",
                    "ssm", "list-command-invocations",
                    "--command-id", commandId,
                    "--instance-id", instanceId,
                    "--region", _options.AwsRegion);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[AdDcProvisioner] Error checking SSM status: {ex.Message}");
                throw;
            }

            if (status == "Success")
            {
                Console.WriteLine($"[AdDcProvisioner] ✓ SSM command completed successfully: {operationName}");
                return;
            }
            if (status == "Failed")
            {
                string output = await QueryAwsAsync(
                    "CommandInvocations[0].CommandPlugins[0].Output",
                    "ssm", "list-command-invocations",
                    "--command-id", commandId,
                    "--instance-id", instanceId,
                    "--region", _options.AwsRegion);
                throw new InvalidOperationException($"SSM command failed. Output: {output}");
            }

            if (DateTime.UtcNow - lastLogTime > TimeSpan.FromSeconds(30))
            {
                int elapsed = (int)(DateTime.UtcNow - start).TotalSeconds;
                Console.WriteLine($"[AdDcProvisioner] SSM in progress [{status}]... ({elapsed}s elapsed, {1800 - elapsed}s remaining)");
                lastLogTime = DateTime.UtcNow;
            }

            await Task.Delay(10000);
        }
        throw new TimeoutException("SSM command execution timeout (30 min)");
    }

    private string WriteSsmCommandPayload(string instanceId, string script)
    {
        var payload = new
        {
            DocumentName = "AWS-RunPowerShellScript",
            InstanceIds = new[] { instanceId },
            Parameters = new Dictionary<string, string[]>
            {
                ["commands"] = new[] { script }
            }
        };

        string path = Path.Combine(
            Path.GetTempPath(),
            $"coreapi-ad-ds-ssm-{Guid.NewGuid():N}.json");
        string json = JsonSerializer.Serialize(payload);
        File.WriteAllText(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return path;
    }

    private static void TryDeleteTempFile(string path)
    {
        try { File.Delete(path); }
        catch { /* Best effort cleanup only. */ }
    }

    private async Task VerifyAdministratorCredentialsAsync(string host)
    {
        string user = $"Administrator@{_options.DomainName}";
        if (await CanBindAsync(host, user, _options.AdAdminPassword))
            return;

        throw new InvalidOperationException(
            $"LDAP port 389 is open on {host}, but binding as {user} failed. " +
            "For Spec 0, a forgotten or mismatched AD password is not repaired in place. " +
            "Redeploy/recreate the test DC with tools\\setup-test-dc.ps1 so configuration and domain state match.");
    }

    private static async Task<bool> CanBindAsync(string host, string user, string password)
    {
        try
        {
            var identifier = new LdapDirectoryIdentifier(host, 389);
            using var conn = new LdapConnection(identifier);
            conn.Credential = new NetworkCredential(user, password);
            conn.AuthType = AuthType.Basic;
            conn.SessionOptions.ProtocolVersion = 3;
            await Task.Run(conn.Bind);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string EscapePowerShellSingleQuotedString(string value) =>
        value.Replace("'", "''");

    private string BuildUserDataScript() => $$"""
        <powershell>
        $ErrorActionPreference = 'Stop'
        $logFile = 'C:\AddsSetup.log'

        try {
            Add-Content $logFile "$(Get-Date): Starting unattended AD DS deployment"

            # ════════════════════════════════════════════════════════════════════════
            # Step 1: Network Configuration (CRITICAL: DNS requires static IP)
            # ════════════════════════════════════════════════════════════════════════
            Add-Content $logFile "$(Get-Date): [1/6] Configuring network with static IP..."
            $adapter = Get-NetAdapter | Where-Object {$_.Status -eq 'Up'} | Select-Object -First 1
            if (-not $adapter) { throw "No active network adapter found" }

            $ifIndex = $adapter.InterfaceIndex
            $currentConfig = Get-NetIPConfiguration -InterfaceIndex $ifIndex
            $currentIp = $currentConfig.IPv4Address[0].IPAddress
            $currentGateway = $currentConfig.IPv4DefaultGateway[0].NextHop

            # Disable DHCP and set static IP
            Set-NetIPInterface -InterfaceIndex $ifIndex -DHCP Disabled
            Remove-NetIPAddress -InterfaceIndex $ifIndex -AddressFamily IPv4 -Confirm:$false -ErrorAction SilentlyContinue
            New-NetIPAddress -InterfaceIndex $ifIndex -IPAddress $currentIp -AddressFamily IPv4 -PrefixLength 24 | Out-Null
            New-NetRoute -InterfaceIndex $ifIndex -DestinationPrefix "0.0.0.0/0" -NextHop $currentGateway -ErrorAction SilentlyContinue | Out-Null

            # Set DNS to point to self first (127.0.0.1) - CRITICAL for DNS service startup
            Set-DnsClientServerAddress -InterfaceIndex $ifIndex -ServerAddresses @("127.0.0.1", "8.8.8.8") | Out-Null
            Add-Content $logFile "$(Get-Date): Network configured - Static IP: $currentIp, DNS: 127.0.0.1"

            # ════════════════════════════════════════════════════════════════════════
            # Step 2: Install DNS Server role
            # ════════════════════════════════════════════════════════════════════════
            Add-Content $logFile "$(Get-Date): [2/6] Installing DNS Server role..."
            Install-WindowsFeature DNS -IncludeManagementTools | Out-Null
            Add-Content $logFile "$(Get-Date): DNS Server role installed"

            # ════════════════════════════════════════════════════════════════════════
            # Step 3: Install AD-Domain-Services role
            # ════════════════════════════════════════════════════════════════════════
            Add-Content $logFile "$(Get-Date): [3/6] Installing AD-Domain-Services role..."
            Install-WindowsFeature AD-Domain-Services -IncludeManagementTools | Out-Null
            Add-Content $logFile "$(Get-Date): AD-Domain-Services role installed"

            # ════════════════════════════════════════════════════════════════════════
            # Step 4: Import AD DS deployment tooling
            # ════════════════════════════════════════════════════════════════════════
            Add-Content $logFile "$(Get-Date): [4/6] Importing ADDSDeployment module..."
            Import-Module ADDSDeployment
            Add-Content $logFile "$(Get-Date): ADDSDeployment module imported"

            # ════════════════════════════════════════════════════════════════════════
            # Step 5: Promote to a new forest
            # ════════════════════════════════════════════════════════════════════════
            Add-Content $logFile "$(Get-Date): [5/6] Promoting server to a new AD DS forest..."
            Add-Content $logFile "$(Get-Date): Domain: {{_options.DomainName}}, NetBIOS: {{_options.DomainNetbiosName}}"

            Add-Content $logFile "$(Get-Date): Setting local Administrator password before promotion..."
            net user Administrator '{{EscapePowerShellSingleQuotedString(_options.AdAdminPassword)}}' /active:yes
            if ($LASTEXITCODE -ne 0) {
                throw "net user Administrator password setup failed with exit code $LASTEXITCODE"
            }

            $safeModePassword = ConvertTo-SecureString '{{EscapePowerShellSingleQuotedString(_options.AdAdminPassword)}}' -AsPlainText -Force
            Install-ADDSForest `
                -DomainName "{{_options.DomainName}}" `
                -DomainNetbiosName "{{_options.DomainNetbiosName}}" `
                -SafeModeAdministratorPassword $safeModePassword `
                -InstallDns `
                -CreateDnsDelegation:$false `
                -DatabasePath "C:\Windows\NTDS" `
                -LogPath "C:\Windows\NTDS" `
                -SysvolPath "C:\Windows\SYSVOL" `
                -ForestMode Win2012R2 `
                -DomainMode Win2012R2 `
                -NoRebootOnCompletion:$true `
                -Force | Out-String | Add-Content $logFile

            Add-Content $logFile "$(Get-Date): [6/6] Forest promotion completed successfully. System will reboot."
            Add-Content $logFile "$(Get-Date): Scheduling reboot so SSM can report success before restart. Expect DC online in 2-5 minutes after reboot."
            shutdown.exe /r /t 20 /c "CoreApi AD DS promotion complete"

        } catch {
            Add-Content $logFile "$(Get-Date): ❌ ERROR: $_"
            Add-Content $logFile "$(Get-Date): Stack trace: $($_.ScriptStackTrace)"
            Add-Content $logFile "$(Get-Date): Check C:\AddsSetup.log and C:\Windows\debug\dcpromo.log for details"
            throw
        }
        </powershell>
        """;

    private async Task WaitForInstanceStateAsync(string instanceId, string targetState)
    {
        Console.WriteLine($"[AdDcProvisioner] Waiting for instance {instanceId} to reach state: {targetState}...");
        DateTime startTime = DateTime.UtcNow;
        DateTime lastLogTime = startTime;

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        while (!cts.Token.IsCancellationRequested)
        {
            string state = await QueryAwsAsync("Reservations[0].Instances[0].State.Name",
                "ec2", "describe-instances", "--instance-ids", instanceId,
                "--region", _options.AwsRegion);
            if (state == targetState)
            {
                // State is running, but wait for both status checks to pass before returning.
                // Instance can be "running" but still "initializing" on status checks.
                await WaitForInstanceStatusChecksAsync(instanceId);
                return;
            }

            // Show progress every 30 seconds
            if (DateTime.UtcNow - lastLogTime > TimeSpan.FromSeconds(30))
            {
                int elapsed = (int)(DateTime.UtcNow - startTime).TotalSeconds;
                Console.WriteLine($"[AdDcProvisioner] Still waiting... (current state: {state}, {elapsed}s elapsed)");
                lastLogTime = DateTime.UtcNow;
            }

            try { await Task.Delay(TimeSpan.FromSeconds(10), cts.Token); }
            catch (OperationCanceledException)
            {
                throw new TimeoutException(
                    $"Instance {instanceId} did not reach state '{targetState}' within 10 minutes.");
            }
        }
    }

    private async Task WaitForInstanceStatusChecksAsync(string instanceId)
    {
        Console.WriteLine($"[AdDcProvisioner] Waiting for instance {instanceId} status checks (system/instance)...");
        DateTime startTime = DateTime.UtcNow;
        DateTime lastLogTime = startTime;

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        while (!cts.Token.IsCancellationRequested)
        {
            string systemStatus = await QueryAwsAsync(
                "InstanceStatuses[0].SystemStatus.Status",
                "ec2", "describe-instance-status", "--instance-ids", instanceId,
                "--include-all-instances", "--region", _options.AwsRegion);

            string instanceStatus = await QueryAwsAsync(
                "InstanceStatuses[0].InstanceStatus.Status",
                "ec2", "describe-instance-status", "--instance-ids", instanceId,
                "--include-all-instances", "--region", _options.AwsRegion);

            if (systemStatus == "ok" && instanceStatus == "ok")
            {
                Console.WriteLine($"[AdDcProvisioner] ✓ Instance {instanceId} status checks passed ({systemStatus}/{instanceStatus})");
                return;
            }

            // Show progress every 30 seconds
            if (DateTime.UtcNow - lastLogTime > TimeSpan.FromSeconds(30))
            {
                int elapsed = (int)(DateTime.UtcNow - startTime).TotalSeconds;
                Console.WriteLine($"[AdDcProvisioner] Still waiting... (system: {systemStatus}, instance: {instanceStatus}, {elapsed}s elapsed)");
                lastLogTime = DateTime.UtcNow;
            }

            try { await Task.Delay(TimeSpan.FromSeconds(10), cts.Token); }
            catch (OperationCanceledException)
            {
                throw new TimeoutException(
                    $"Instance {instanceId} status checks did not pass within 10 minutes. " +
                    $"System: {systemStatus}, Instance: {instanceStatus}");
            }
        }
    }

    private async Task<string> WaitForPublicIpAsync(string instanceId)
    {
        Console.WriteLine($"[AdDcProvisioner] Waiting for public IP on instance {instanceId}...");
        DateTime startTime = DateTime.UtcNow;
        DateTime lastLogTime = startTime;

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        while (!cts.Token.IsCancellationRequested)
        {
            string ip = await QueryAwsAsync("Reservations[0].Instances[0].PublicIpAddress",
                "ec2", "describe-instances", "--instance-ids", instanceId,
                "--region", _options.AwsRegion);
            if (!string.IsNullOrEmpty(ip) && ip != "None") return ip;

            // Show progress every 30 seconds
            if (DateTime.UtcNow - lastLogTime > TimeSpan.FromSeconds(30))
            {
                int elapsed = (int)(DateTime.UtcNow - startTime).TotalSeconds;
                Console.WriteLine($"[AdDcProvisioner] Still waiting for IP... ({elapsed}s elapsed)");
                lastLogTime = DateTime.UtcNow;
            }

            try { await Task.Delay(TimeSpan.FromSeconds(5), cts.Token); }
            catch (OperationCanceledException)
            {
                throw new TimeoutException(
                    $"Instance {instanceId} did not receive a public IP within 2 minutes.");
            }
        }
        throw new TimeoutException("Instance did not get public IP within timeout.");
    }

    private async Task AssociateElasticIpWithRetryAsync(string instanceId, string allocationId)
    {
        int maxRetries = 5;
        int retryDelaySeconds = 10;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                Console.WriteLine($"[AdDcProvisioner] Associating Elastic IP - attempt {attempt}/{maxRetries}");
                await AssociateElasticIpAsync(instanceId, allocationId);
                return;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("not in a valid state"))
            {
                if (attempt < maxRetries)
                {
                    Console.WriteLine($"[AdDcProvisioner] Instance not ready yet, retrying in {retryDelaySeconds}s...");
                    await Task.Delay(TimeSpan.FromSeconds(retryDelaySeconds));
                }
                else
                {
                    throw;
                }
            }
        }
    }

    private async Task AssociateElasticIpAsync(string instanceId, string allocationId)
    {
        await InvokeAwsAsync("ec2", "associate-address", "--instance-id", instanceId,
            "--allocation-id", allocationId, "--allow-reassociation",
            "--region", _options.AwsRegion);
    }

    private async Task StopInstanceAsync(string instanceId)
    {
        await InvokeAwsAsync("ec2", "stop-instances", "--instance-ids", instanceId,
            "--region", _options.AwsRegion);
    }

    private static async Task<bool> CanConnectTcpAsync(string host, int port, TimeSpan timeout)
    {
        try
        {
            using var cts = new CancellationTokenSource(timeout);
            using var tcp = new TcpClient();
            await tcp.ConnectAsync(host, port, cts.Token);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // -- Validation --

    private static void ValidateRequiredOptions(AdDcProvisionerOptions opts)
    {
        var missing = new List<string>();

        if (string.IsNullOrEmpty(opts.AmiId) && string.IsNullOrEmpty(opts.ExistingInstanceId))
            missing.Add("AmiId (or ExistingInstanceId for an existing DC)");
        if (string.IsNullOrEmpty(opts.SecurityGroupId))
            missing.Add("SecurityGroupId");
        if (string.IsNullOrEmpty(opts.AdAdminPassword))
            missing.Add("AdAdminPassword");

        if (missing.Count > 0)
            throw new InvalidOperationException(
                "EC2 provisioner is enabled (ProvisionAdDc=true) but required settings are missing: " +
                string.Join(", ", missing) + ". " +
                "Run 'tools\\setup-test-dc.ps1' from the repository root to configure the environment.");
    }

    private void WarnIfNoLdapTarget()
    {
        bool hasHost = !string.IsNullOrEmpty(ResolvedHost) && ResolvedHost != "localhost";
        bool hasUser = !string.IsNullOrEmpty(ResolvedServiceAccountUser);
        if (!hasHost || !hasUser)
            Console.WriteLine(
                "[AdDcProvisioner] WARNING: ProvisionAdDc=false and LDAP__Host / LDAP__ServiceAccountUser " +
                "are not set. Integration tests will fail to connect. " +
                "Run 'tools\\setup-test-dc.ps1' or set LDAP__* env vars.");
    }

    // -- Seed --

    private void SeedDemoData()
    {
        Thread.Sleep(TimeSpan.FromSeconds(10));
        var identifier = new LdapDirectoryIdentifier(ResolvedHost, ResolvedPort);
        using var conn = new LdapConnection(identifier);
        conn.Credential = new NetworkCredential(ResolvedServiceAccountUser, ResolvedServiceAccountPassword);
        conn.AuthType = AuthType.Basic;
        conn.SessionOptions.ProtocolVersion = 3;
        conn.Bind();

        string dn = ResolvedBaseDn;

        AddOu(conn, "Users", dn);
        AddOu(conn, "ServiceAccounts", dn);
        AddOu(conn, "Groups", dn);

        AddUser(conn, "alice.martin",    "Alice",   "Martin",   $"OU=Users,{dn}");
        AddUser(conn, "bob.dupont",      "Bob",     "Dupont",   $"OU=Users,{dn}");
        AddUser(conn, "claire.bernard",  "Claire",  "Bernard",  $"OU=Users,{dn}");

        AddGroup(conn, "IT-Admins", $"OU=Groups,{dn}");
        AddGroup(conn, "Dev-Team",  $"OU=Groups,{dn}");

        AddUser(conn, "svc-coreapi", "CoreApi", "Service", $"OU=ServiceAccounts,{dn}");

        Console.WriteLine("[AdDcProvisioner] Demo data seeded successfully.");
    }

    private static void AddOu(LdapConnection conn, string ouName, string parentDn)
    {
        string dn = $"OU={ouName},{parentDn}";
        var req = new AddRequest(dn, new DirectoryAttribute[]
        {
            new("objectClass", "organizationalUnit"),
            new("ou", ouName)
        });
        SendIgnoringAlreadyExists(conn, req);
    }

    private static void AddUser(LdapConnection conn, string samAccount, string givenName,
        string surname, string parentDn)
    {
        string cn = $"{givenName} {surname}";
        string dn = $"CN={cn},{parentDn}";
        string domain = ExtractDomain(parentDn);

        var req = new AddRequest(dn, new DirectoryAttribute[]
        {
            new("objectClass", "user"),
            new("sAMAccountName", samAccount),
            new("userPrincipalName", $"{samAccount}@{domain}"),
            new("givenName", givenName),
            new("sn", surname),
            new("displayName", cn),
            new("userAccountControl", "514")
        });
        SendIgnoringAlreadyExists(conn, req);
    }

    private static void AddGroup(LdapConnection conn, string groupName, string parentDn)
    {
        string dn = $"CN={groupName},{parentDn}";
        var req = new AddRequest(dn, new DirectoryAttribute[]
        {
            new("objectClass", "group"),
            new("sAMAccountName", groupName),
            new("groupType", "-2147483646")
        });
        SendIgnoringAlreadyExists(conn, req);
    }

    private static void SendIgnoringAlreadyExists(LdapConnection conn, DirectoryRequest req)
    {
        try { conn.SendRequest(req); }
        catch (DirectoryOperationException ex)
            when (ex.Response?.ResultCode == ResultCode.EntryAlreadyExists) { }
    }

    private static string ExtractDomain(string dn)
    {
        var parts = dn.Split(',')
            .Where(p => p.StartsWith("DC=", StringComparison.OrdinalIgnoreCase))
            .Select(p => p[3..]);
        return string.Join(".", parts);
    }

    // -- Config and helpers --

    private static AdDcProvisionerOptions LoadOptions()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables("TESTINFRA__")
            .Build();

        return config.GetSection("TestInfrastructure").Get<AdDcProvisionerOptions>()
            ?? new AdDcProvisionerOptions();
    }

    private async Task WaitForLdapAsync(string host)
    {
        using var cts = new CancellationTokenSource(
            TimeSpan.FromSeconds(_options.LdapReadyTimeoutSeconds));

        DateTime startTime = DateTime.UtcNow;
        DateTime lastLogTime = startTime;

        while (true)
        {
            try
            {
                using var tcp = new TcpClient();
                await tcp.ConnectAsync(host, 389, cts.Token);
                return;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine(
                    $"[AdDcProvisioner] LDAP port 389 on {host} did not open within {_options.LdapReadyTimeoutSeconds}s.");
                Console.WriteLine("[AdDcProvisioner] Check UserData logs on the instance: C:\\AddsSetup.log");
                if (!string.IsNullOrEmpty(_launchedInstanceId))
                {
                    await PrintInstanceSetupLogsAsync(_launchedInstanceId);
                }
                throw new TimeoutException(
                    $"LDAP port 389 on {host} did not open within {_options.LdapReadyTimeoutSeconds}s. " +
                    "The DC may still be promoting -- increase LdapReadyTimeoutSeconds or check EC2 console.");
            }
            catch
            {
                try
                {
                    // Show progress every 30 seconds
                    if (DateTime.UtcNow - lastLogTime > TimeSpan.FromSeconds(30))
                    {
                        int elapsed = (int)(DateTime.UtcNow - startTime).TotalSeconds;
                        Console.WriteLine($"[AdDcProvisioner] Still waiting... ({elapsed}s elapsed, {_options.LdapReadyTimeoutSeconds}s timeout)");
                        lastLogTime = DateTime.UtcNow;
                    }

                    await Task.Delay(TimeSpan.FromSeconds(15), cts.Token);
                }
                catch (OperationCanceledException)
                {
                    throw new TimeoutException(
                        $"LDAP port 389 on {host} did not open within {_options.LdapReadyTimeoutSeconds}s.");
                }
            }
        }
    }

    private async Task PrintInstanceSetupLogsAsync(string instanceId)
    {
        try
        {
            Console.WriteLine("\n[AdDcProvisioner] Attempting to retrieve UserData logs from instance...");
            await Task.CompletedTask;
            // Note: This would require SSM Session Manager or RDP access to retrieve the file.
            // For now, we can only suggest manual inspection via EC2 console or Systems Manager.
            Console.WriteLine("[AdDcProvisioner] To inspect logs, use Systems Manager Session Manager or RDP into the instance.");
        }
        catch
        {
            // Silently fail — log retrieval not critical
        }
    }

    private static string DomainNameToBaseDn(string domainName) =>
        "DC=" + domainName.Replace(".", ",DC=");
}
