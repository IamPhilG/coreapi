using System.DirectoryServices.Protocols;
using CoreApi.Infrastructure;
using CoreApi.Infrastructure.Observability;
using CoreApi.UnitTests.TestInfrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoreApi.UnitTests.Observability;

/// <summary>
/// Proves LDAP operations emit a structured log with a duration and outcome without a DC, by
/// pointing the connection at a closed local port (no directory involved) and asserting the
/// failure log. The filter/DN are never logged in clear -- only an operation category, host,
/// transport, duration, and LDAP code.
/// </summary>
public class LdapDirectoryConnectionLoggingTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public async Task SearchAsync_against_a_closed_port_logs_a_structured_failure_with_duration()
    {
        var collector = new InMemoryLogCollector();
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Debug);
            builder.AddProvider(collector);
        });

        var options = Options.Create(new DirectoryConnectionOptions
        {
            Host = "127.0.0.1",
            Port = 65533, // Nothing listens here -> connection refused, a fast LdapException.
            BaseDn = "DC=corp,DC=local",
            UseTls = false,
            TimeoutSeconds = 2,
        });

        using var connection = new LdapDirectoryConnection(
            options, loggerFactory.CreateLogger<LdapDirectoryConnection>(), TestPseudonymizer.Create());

        await Assert.ThrowsAsync<LdapException>(() =>
            connection.SearchAsync("DC=corp,DC=local", "(objectClass=*)", SearchScope.Subtree));

        LogRecord failure = Assert.Single(
            collector.Records, r => r.EventId == ObservabilityEvents.LdapOperationFailed);

        Assert.Equal(LogLevel.Warning, failure.Level);
        Assert.Equal("Search", (string?)failure.State.Single(kv => kv.Key == "LdapOperation").Value);
        Assert.Equal("LDAP", (string?)failure.State.Single(kv => kv.Key == "LdapTransport").Value);
        Assert.Equal("127.0.0.1", (string?)failure.State.Single(kv => kv.Key == "LdapHost").Value);
        Assert.Equal("LdapException", (string?)failure.State.Single(kv => kv.Key == "ExceptionType").Value);
        Assert.Contains(failure.State, kv => kv.Key == "ElapsedMilliseconds");
        Assert.Contains(failure.State, kv => kv.Key == "LdapCode");   // useful code retained
        Assert.Contains(failure.State, kv => kv.Key == "ErrorCategory");

        // The raw exception is never attached to the logger, and neither the filter nor the base
        // DN appears in clear.
        Assert.Null(failure.Exception);
        Assert.False(collector.AnyTextContains("(objectClass=*)"));
        Assert.False(collector.AnyTextContains("DC=corp,DC=local"));
    }
}
