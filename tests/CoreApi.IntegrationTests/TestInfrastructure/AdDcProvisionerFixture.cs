using System.Net.Sockets;
using System.Text;
using Amazon;
using Amazon.EC2;
using Amazon.EC2.Model;
using Microsoft.Extensions.Configuration;

namespace CoreApi.IntegrationTests.TestInfrastructure;

/// <summary>
/// xUnit collection fixture that optionally provisions an EC2 Windows Server AD DS instance
/// before integration tests run and stops it on teardown.
///
/// When TestInfrastructure:ProvisionAdDc is false (the default), no AWS calls are made and
/// the resolved connection parameters fall through to LDAP__* environment variables, exactly
/// as the tests worked before this fixture existed.
///
/// Configuration sources (env vars take precedence):
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
    private AmazonEC2Client? _ec2;
    private string? _launchedInstanceId;

    public async Task InitializeAsync()
    {
        _options = LoadOptions();

        if (!_options.ProvisionAdDc)
            return;

        _ec2 = new AmazonEC2Client(RegionEndpoint.GetBySystemName(_options.AwsRegion));

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
                $"Add this as TestInfrastructure:ExistingInstanceId to reuse it on subsequent runs.");
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
    }

    public async Task DisposeAsync()
    {
        if (_ec2 is null)
            return;

        string? toStop = _launchedInstanceId
            ?? (_options.ProvisionAdDc && !string.IsNullOrEmpty(_options.ExistingInstanceId)
                ? _options.ExistingInstanceId
                : null);

        if (toStop is not null)
        {
            await _ec2.StopInstancesAsync(new StopInstancesRequest
            {
                InstanceIds = [toStop]
            });
        }

        _ec2.Dispose();
    }

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

    private async Task StartExistingInstanceAsync(string instanceId)
    {
        await _ec2!.StartInstancesAsync(new StartInstancesRequest
        {
            InstanceIds = [instanceId]
        });
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

        string userDataBase64 = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(BuildUserDataScript()));

        var request = new RunInstancesRequest
        {
            ImageId = _options.AmiId,
            InstanceType = new InstanceType(_options.InstanceType),
            MinCount = 1,
            MaxCount = 1,
            UserData = userDataBase64,
            TagSpecifications =
            [
                new TagSpecification
                {
                    ResourceType = ResourceType.Instance,
                    Tags =
                    [
                        new Tag("Name", "coreapi-test-dc"),
                        new Tag("coreapi-managed", "true")
                    ]
                }
            ]
        };

        if (!string.IsNullOrEmpty(_options.SecurityGroupId))
            request.SecurityGroupIds = [_options.SecurityGroupId];
        if (!string.IsNullOrEmpty(_options.SubnetId))
            request.SubnetId = _options.SubnetId;
        if (!string.IsNullOrEmpty(_options.KeyPairName))
            request.KeyName = _options.KeyPairName;

        var response = await _ec2!.RunInstancesAsync(request);
        string instanceId = response.Reservation.Instances[0].InstanceId;

        await WaitForInstanceStateAsync(instanceId, "running");
        return instanceId;
    }

    private string BuildUserDataScript() => $"""
        <powershell>
        $ErrorActionPreference = 'Stop'
        Install-WindowsFeature AD-Domain-Services -IncludeManagementTools
        Import-Module ADDSDeployment
        $password = ConvertTo-SecureString '{_options.AdAdminPassword}' -AsPlainText -Force
        Install-ADDSForest `
            -DomainName '{_options.DomainName}' `
            -DomainNetbiosName '{_options.DomainNetbiosName}' `
            -SafeModeAdministratorPassword $password `
            -InstallDns `
            -Force
        </powershell>
        """;

    private async Task WaitForInstanceStateAsync(string instanceId, string targetState)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));
        while (true)
        {
            var desc = await _ec2!.DescribeInstancesAsync(
                new DescribeInstancesRequest { InstanceIds = [instanceId] }, cts.Token);

            string state = desc.Reservations[0].Instances[0].State.Name.Value;
            if (state == targetState) return;

            try { await Task.Delay(TimeSpan.FromSeconds(10), cts.Token); }
            catch (OperationCanceledException)
            {
                throw new TimeoutException(
                    $"Instance {instanceId} did not reach state '{targetState}' within 10 minutes.");
            }
        }
    }

    private async Task<string> WaitForPublicIpAsync(string instanceId)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        while (true)
        {
            var desc = await _ec2!.DescribeInstancesAsync(
                new DescribeInstancesRequest { InstanceIds = [instanceId] }, cts.Token);

            string? ip = desc.Reservations[0].Instances[0].PublicIpAddress;
            if (!string.IsNullOrEmpty(ip)) return ip;

            try { await Task.Delay(TimeSpan.FromSeconds(5), cts.Token); }
            catch (OperationCanceledException)
            {
                throw new TimeoutException(
                    $"Instance {instanceId} did not receive a public IP within 2 minutes.");
            }
        }
    }

    private async Task AssociateElasticIpAsync(string instanceId, string allocationId)
    {
        await _ec2!.AssociateAddressAsync(new AssociateAddressRequest
        {
            InstanceId = instanceId,
            AllocationId = allocationId,
            AllowReassociation = true
        });
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
                throw new TimeoutException(
                    $"LDAP port 389 on {host} did not open within {_options.LdapReadyTimeoutSeconds}s. " +
                    "The DC may still be promoting — increase LdapReadyTimeoutSeconds or check EC2 console.");
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

    private static string DomainNameToBaseDn(string domainName) =>
        "DC=" + domainName.Replace(".", ",DC=");
}
