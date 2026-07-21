using CoreApi.Infrastructure;
using CoreApi.UnitTests.TestInfrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CoreApi.UnitTests.Infrastructure;

public class LdapDirectoryConnectionTests
{
    private static LdapDirectoryConnection Build(Action<DirectoryConnectionOptions>? configure = null)
    {
        var opt = new DirectoryConnectionOptions
        {
            Host = "ldap.test.local",
            BaseDn = "DC=test,DC=local",
            UseTls = false // avoid cert callback in unit tests
        };
        configure?.Invoke(opt);
        return new LdapDirectoryConnection(
            Options.Create(opt), NullLogger<LdapDirectoryConnection>.Instance, TestPseudonymizer.Create());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_does_not_throw_for_valid_options()
    {
        // AutoBind=false means no network call at construction — should succeed regardless of host reachability
        using var conn = Build();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_does_not_throw_when_ServiceAccountUser_is_empty()
    {
        using var conn = Build(o =>
        {
            o.ServiceAccountUser = string.Empty;
            o.ServiceAccountPassword = string.Empty;
        });
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_does_not_throw_when_credentials_are_provided()
    {
        using var conn = Build(o =>
        {
            o.ServiceAccountUser = "svc-coreapi";
            o.ServiceAccountPassword = "P@ssw0rd!";
        });
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Dispose_can_be_called_multiple_times_without_throwing()
    {
        var conn = Build();
        conn.Dispose();
        // Second Dispose must not throw — Dispose should be idempotent
        conn.Dispose();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task SearchAsync_after_Dispose_throws_ObjectDisposedException()
    {
        var conn = Build();
        conn.Dispose();
        await Assert.ThrowsAnyAsync<Exception>(() =>
            conn.SearchAsync(string.Empty, "(objectClass=*)", System.DirectoryServices.Protocols.SearchScope.Base));
    }
}
