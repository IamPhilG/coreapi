namespace CoreApi.IntegrationTests.TestInfrastructure;

public sealed class AdDcProvisionerOptions
{
    public bool ProvisionAdDc { get; init; }
    public string AwsRegion { get; init; } = "us-east-1";
    public string InstanceType { get; init; } = "t3.medium";
    public string AmiId { get; init; } = string.Empty;
    public string SecurityGroupId { get; init; } = string.Empty;
    public string SubnetId { get; init; } = string.Empty;
    public string KeyPairName { get; init; } = string.Empty;
    public string ElasticIpAllocationId { get; init; } = string.Empty;

    /// <summary>
    /// When set, the fixture starts this existing instance instead of launching a new one.
    /// Populate after the first run so subsequent runs reuse the same DC (faster + cheaper).
    /// </summary>
    public string ExistingInstanceId { get; init; } = string.Empty;

    public string DomainName { get; init; } = "corp.local";
    public string DomainNetbiosName { get; init; } = "CORP";

    /// <summary>
    /// Password for the AD Administrator account and the DSRM safe-mode password.
    /// Must satisfy Windows Server password complexity requirements.
    /// Never commit a non-empty value — supply via env var TESTINFRA__AdAdminPassword.
    /// </summary>
    public string AdAdminPassword { get; init; } = string.Empty;

    /// <summary>Seconds to wait for LDAP port 389 to open after the instance starts. Default 900 (15 min).</summary>
    public int LdapReadyTimeoutSeconds { get; init; } = 900;
}
