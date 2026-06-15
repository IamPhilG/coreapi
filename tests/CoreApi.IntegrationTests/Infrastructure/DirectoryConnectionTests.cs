using System.DirectoryServices.Protocols;
using CoreApi.Infrastructure;
using CoreApi.IntegrationTests.TestInfrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CoreApi.IntegrationTests.Infrastructure;

/// <summary>
/// Requires a real LDAP target. Two ways to provide one:
///
/// Option A — Manual (existing DC or Samba container):
///   Set env vars: LDAP__Host, LDAP__BaseDn, LDAP__Port, LDAP__UseTls,
///                 LDAP__ServiceAccountUser, LDAP__ServiceAccountPassword
///
/// Option B — Auto-provisioned EC2 (set in tests/CoreApi.IntegrationTests/appsettings.Development.json):
///   TestInfrastructure:ProvisionAdDc = true
///   TestInfrastructure:ExistingInstanceId = i-xxxx  (or leave empty to launch fresh)
///   See appsettings.Development.template.json for all options.
/// </summary>
[Collection("AdDc")]
public class DirectoryConnectionTests : IDisposable
{
    private readonly LdapDirectoryConnection _connection;
    private readonly string _baseDn;

    public DirectoryConnectionTests(AdDcProvisionerFixture provisioner)
    {
        var options = new DirectoryConnectionOptions
        {
            Host = provisioner.ResolvedHost,
            BaseDn = provisioner.ResolvedBaseDn,
            Port = provisioner.ResolvedPort,
            UseTls = provisioner.ResolvedUseTls,
            ServiceAccountUser = provisioner.ResolvedServiceAccountUser,
            ServiceAccountPassword = provisioner.ResolvedServiceAccountPassword,
            TimeoutSeconds = 10
        };

        _baseDn = options.BaseDn;
        _connection = new LdapDirectoryConnection(
            Options.Create(options),
            NullLogger<LdapDirectoryConnection>.Instance);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SearchAsync_RootDse_ReturnsDefaultNamingContext()
    {
        var results = await _connection.SearchAsync(
            baseDn: string.Empty,
            filter: "(objectClass=*)",
            scope: SearchScope.Base,
            attributes: ["defaultNamingContext", "dsServiceName", "supportedSASLMechanisms"]);

        Assert.Single(results);
        Assert.NotNull(results[0].Attributes["defaultNamingContext"]);
        Assert.NotNull(results[0].Attributes["dsServiceName"]);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task SearchAsync_DomainRoot_ReturnsAtLeastOneEntry()
    {
        var results = await _connection.SearchAsync(
            baseDn: _baseDn,
            filter: "(objectClass=domain)",
            scope: SearchScope.Base,
            attributes: ["dc", "distinguishedName"]);

        Assert.NotEmpty(results);
    }

    public void Dispose() => _connection.Dispose();
}
