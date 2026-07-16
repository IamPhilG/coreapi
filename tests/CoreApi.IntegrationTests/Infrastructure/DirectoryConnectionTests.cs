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

    [Fact]
    [Trait("Category", "Integration")]
    public async Task AddModifyMoveDelete_FullLifecycle_AgainstRealDc()
    {
        // sAMAccountName is capped at 20 chars, so keep the random suffix short.
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string cn = $"itest-{suffix}";
        string usersContainer = $"CN=Users,{_baseDn}";
        string dn = $"CN={cn},{usersContainer}";

        // Add — userAccountControl=514 (disabled + normal account) avoids touching
        // unicodePwd/password-complexity, which requires LDAPS and is out of scope here.
        await _connection.AddAsync(new AddRequest(dn,
            new DirectoryAttribute("objectClass", "user"),
            new DirectoryAttribute("sAMAccountName", cn),
            new DirectoryAttribute("userAccountControl", "514")));

        try
        {
            var afterAdd = await _connection.SearchAsync(
                dn, "(objectClass=user)", SearchScope.Base, ["cn"]);
            Assert.Single(afterAdd);

            // Modify
            await _connection.ModifyAsync(new ModifyRequest(
                dn, DirectoryAttributeOperation.Replace, "description", "coreapi integration test"));

            var afterModify = await _connection.SearchAsync(
                dn, "(objectClass=user)", SearchScope.Base, ["description"]);
            Assert.Equal("coreapi integration test", (string)afterModify[0].Attributes["description"][0]);

            // Move (rename in place — newParentDn=null keeps the same container)
            string renamedCn = $"{cn}-renamed";
            string renamedDn = $"CN={renamedCn},{usersContainer}";
            await _connection.MoveAsync(dn, newParentDn: null, newName: $"CN={renamedCn}");
            dn = renamedDn;

            var afterMove = await _connection.SearchAsync(
                dn, "(objectClass=user)", SearchScope.Base, ["cn"]);
            Assert.Single(afterMove);

            var oldDnGone = await _connection.SearchAsync(
                usersContainer, $"(cn={LdapFilterEncoder.Escape(cn)})", SearchScope.OneLevel, ["cn"]);
            Assert.Empty(oldDnGone);
        }
        finally
        {
            await _connection.DeleteAsync(dn);
        }

        var afterDelete = await _connection.SearchAsync(
            usersContainer, $"(cn={LdapFilterEncoder.Escape(cn)}*)", SearchScope.OneLevel, ["cn"]);
        Assert.Empty(afterDelete);
    }

    public void Dispose() => _connection.Dispose();
}
