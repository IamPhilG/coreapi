using System.Diagnostics;
using System.DirectoryServices.Protocols;
using System.Net;
using System.Net.Sockets;
using System.Text;
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
            await StartExistingInstanceAsync(instanceId);
        }
        else
        {
            instanceId = await LaunchNewInstanceAsync();
            _launchedInstanceId = instanceId;
            Console.WriteLine(
                $"[AdDcProvisioner] New instance launched: {instanceId}. " +
                "Add this as TestInfrastructure:ExistingInstanceId to reuse it on subsequent runs.");
        }

        string publicIp = await WaitForPublicIpAsync(instanceId);

        if (!string.IsNullOrEmpty(_options.ElasticIpAllocationId))
        {
            await AssociateElasticIpAsync(instanceId, _options.ElasticIpAllocationId);
            publicIp = await WaitForPublicIpAsync(instanceId);
        }

        await WaitForLdapAsync(publicIp);

        ResolvedHost = publicIp;
        ResolvedBaseDn = DomainNameToBaseDn(_options.DomainName);
        ResolvedPort = 389;
        ResolvedUseTls = false;
        ResolvedServiceAccountUser = $"Administrator@{_options.DomainName}";
        ResolvedServiceAccountPassword = _options.AdAdminPassword;

        if (_options.SeedDemoData)
            await Task.Run(SeedDemoData);
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

    private async Task<string> LaunchNewInstanceAsync()
    {
        if (string.IsNullOrEmpty(_options.AmiId))
            throw new InvalidOperationException(
                "TestInfrastructure:AmiId is required when ExistingInstanceId is not set.");
        if (string.IsNullOrEmpty(_options.AdAdminPassword))
            throw new InvalidOperationException(
                "TestInfrastructure:AdAdminPassword is required.");

        // Check if an instance with the Elastic IP is already running (idempotent).
        if (!string.IsNullOrEmpty(_options.ElasticIpAllocationId))
        {
            string? existingId = await TryGetInstanceIdForElasticIpAsync(_options.ElasticIpAllocationId);
            if (existingId is not null)
            {
                Console.WriteLine(
                    $"[AdDcProvisioner] Instance {existingId} already has Elastic IP {_options.ElasticIpAllocationId}. Reusing.");
                return existingId;
            }
        }

        string userDataBase64 = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(BuildUserDataScript()));

        var args = new List<string>
        {
            "ec2", "run-instances",
            "--image-id", _options.AmiId,
            "--instance-type", _options.InstanceType,
            "--security-group-ids", _options.SecurityGroupId,
            "--user-data", userDataBase64,
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
            args.Add($"--iam-instance-profile=Name={_options.IamInstanceProfile}");
        }

        string instanceId = await QueryAwsAsync("Instances[0].InstanceId", args.ToArray());

        await WaitForInstanceStateAsync(instanceId, "running");
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

    private string BuildUserDataScript() => $$"""
        <powershell>
        $ErrorActionPreference = 'Stop'
        $logFile = 'C:\AddsSetup.log'

        try {
            Add-Content $logFile "$(Get-Date): Starting AD DS setup"

            Add-Content $logFile "$(Get-Date): Installing AD DS..."
            Install-WindowsFeature AD-Domain-Services -IncludeManagementTools | Out-String | Add-Content $logFile

            Add-Content $logFile "$(Get-Date): Importing ADDSDeployment module..."
            Import-Module ADDSDeployment

            Add-Content $logFile "$(Get-Date): Creating safe mode password..."
            $password = ConvertTo-SecureString '{{_options.AdAdminPassword}}' -AsPlainText -Force

            Add-Content $logFile "$(Get-Date): Installing AD DS Forest..."
            Install-ADDSForest `
                -DomainName '{{_options.DomainName}}' `
                -DomainNetbiosName '{{_options.DomainNetbiosName}}' `
                -SafeModeAdministratorPassword $password `
                -InstallDns `
                -Force | Out-String | Add-Content $logFile

            Add-Content $logFile "$(Get-Date): AD DS setup completed successfully"
        } catch {
            Add-Content $logFile "$(Get-Date): ERROR: $_"
            Add-Content $logFile "$(Get-Date): Stack trace: $($_.ScriptStackTrace)"
            throw
        }
        </powershell>
        """;

    private async Task WaitForInstanceStateAsync(string instanceId, string targetState)
    {
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
                Console.WriteLine($"[AdDcProvisioner] Instance {instanceId} status checks passed (ok/ok).");
                return;
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
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        while (!cts.Token.IsCancellationRequested)
        {
            string ip = await QueryAwsAsync("Reservations[0].Instances[0].PublicIpAddress",
                "ec2", "describe-instances", "--instance-ids", instanceId,
                "--region", _options.AwsRegion);
            if (!string.IsNullOrEmpty(ip) && ip != "None") return ip;
            try { await Task.Delay(TimeSpan.FromSeconds(5), cts.Token); }
            catch (OperationCanceledException)
            {
                throw new TimeoutException(
                    $"Instance {instanceId} did not receive a public IP within 2 minutes.");
            }
        }
        throw new TimeoutException("Instance did not get public IP within timeout.");
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
                try { await Task.Delay(TimeSpan.FromSeconds(15), cts.Token); }
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
