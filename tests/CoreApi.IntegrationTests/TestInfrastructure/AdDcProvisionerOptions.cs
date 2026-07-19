namespace CoreApi.IntegrationTests.TestInfrastructure;

public sealed class AdDcProvisionerOptions
{
    public bool ProvisionAdDc { get; init; }
    public string AwsRegion { get; init; } = "us-east-1";

    /// <summary>
    /// Named AWS CLI profile to use for all EC2 operations (credential chain + region).
    /// Matches the profile in ~/.aws/credentials or ~/.aws/config.
    /// Leave empty to use the default credential chain (env vars, instance role, default profile).
    /// </summary>
    public string AwsProfile { get; init; } = string.Empty;
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

    /// <summary>
    /// IAM instance profile name to attach to launched instances.
    /// Provides permissions for Systems Manager Session Manager and EC2 Instance Connect.
    /// </summary>
    public string IamInstanceProfile { get; init; } = string.Empty;

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

    /// <summary>
    /// When true, the fixture skips the StopInstances call on teardown so the DC stays running
    /// after the test run. Use for demo environments. Default false (stop on teardown).
    /// </summary>
    public bool KeepRunning { get; init; }

    /// <summary>
    /// When true, seeds the AD directory with realistic demo objects (OUs, users, groups, a
    /// service account) after the DC is ready. Idempotent — safe to run against an already-seeded
    /// DC because existing objects are silently skipped.
    /// Users are created in a disabled state; enabling them requires LDAPS (port 636) to set
    /// unicodePwd, which is outside the scope of this dev fixture.
    /// </summary>
    public bool SeedDemoData { get; init; }
}
