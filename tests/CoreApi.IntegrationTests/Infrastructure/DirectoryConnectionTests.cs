using System.DirectoryServices.Protocols;
using CoreApi.Infrastructure;
using Microsoft.Extensions.Options;

namespace CoreApi.IntegrationTests.Infrastructure;

/// <summary>
/// Requires a real LDAP target. Configure via environment variables before running:
///   LDAP__Host, LDAP__BaseDn, LDAP__Port, LDAP__UseTls,
///   LDAP__ServiceAccountUser, LDAP__ServiceAccountPassword
/// Or populate appsettings.Development.json with real DC values.
/// </summary>
public class DirectoryConnectionTests : IDisposable
{
    private readonly LdapDirectoryConnection _connection;
    private readonly string _baseDn;

    public DirectoryConnectionTests()
    {
        // Partial options object created to extract BaseDn — host and baseDn duplicated below
        var configOptions = new DirectoryConnectionOptions
        {
            Host = Environment.GetEnvironmentVariable("LDAP__Host") ?? "localhost",
            BaseDn = Environment.GetEnvironmentVariable("LDAP__BaseDn") ?? "DC=corp,DC=local",
        };
        _baseDn = configOptions.BaseDn;

        var options = new DirectoryConnectionOptions
        {
            Host = configOptions.Host,
            BaseDn = configOptions.BaseDn,
            Port = int.TryParse(Environment.GetEnvironmentVariable("LDAP__Port"), out var p) ? p : 389,
            UseTls = bool.TryParse(Environment.GetEnvironmentVariable("LDAP__UseTls"), out var tls) && tls,
            ServiceAccountUser = Environment.GetEnvironmentVariable("LDAP__ServiceAccountUser") ?? string.Empty,
            ServiceAccountPassword = Environment.GetEnvironmentVariable("LDAP__ServiceAccountPassword") ?? string.Empty,
            TimeoutSeconds = 10
        };

        _connection = new LdapDirectoryConnection(Options.Create(options));
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
