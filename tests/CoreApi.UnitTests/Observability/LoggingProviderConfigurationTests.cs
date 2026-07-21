using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Options;

namespace CoreApi.UnitTests.Observability;

/// <summary>
/// Regression guard for the logging bootstrap: the default providers must be cleared so exactly
/// one console provider is active (a readable one in Development, JSON elsewhere), with scopes
/// included. Without ClearProviders there would be two console providers (default + ours).
/// </summary>
[Trait("Category", "Unit")]
public sealed class LoggingProviderConfigurationTests
{
    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public void Exactly_one_console_provider_is_active_with_scopes(string environment)
    {
        using var factory = new Factory(environment);

        var providers = factory.Services.GetServices<ILoggerProvider>().ToList();

        Assert.Single(providers, p => p is ConsoleLoggerProvider);
        Assert.DoesNotContain(providers, p => p.GetType().Name.Contains("Debug", StringComparison.Ordinal));
        Assert.DoesNotContain(providers, p => p.GetType().Name.Contains("EventSource", StringComparison.Ordinal));

        bool includeScopes = environment == "Development"
            ? factory.Services.GetRequiredService<IOptionsMonitor<SimpleConsoleFormatterOptions>>().CurrentValue.IncludeScopes
            : factory.Services.GetRequiredService<IOptionsMonitor<JsonConsoleFormatterOptions>>().CurrentValue.IncludeScopes;
        Assert.True(includeScopes);
    }

    private sealed class Factory(string environment) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("Jwt:Authority", "https://dev-sts.coreapi.local");
            builder.UseSetting("Jwt:Audience", "coreapi");
            builder.UseSetting("Jwt:Issuer", "https://dev-sts.coreapi.local");
            builder.UseSetting("DirectoryConnection:Host", "unused.invalid");
            builder.UseSetting("DirectoryConnection:BaseDn", "DC=corp,DC=local");
            builder.UseSetting("DirectoryConnection:UseTls", "true"); // required outside Development
            builder.UseSetting("Observability:PseudonymizationKey", TestInfrastructure.TestPseudonymizer.Key);
        }
    }
}
