using System.Text;
using CoreApi.Infrastructure.Observability;
using CoreApi.UnitTests.TestInfrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace CoreApi.UnitTests.Observability;

/// <summary>Configuration behaviour of the pseudonymization key: required and validated outside
/// Development/Test, and never leaked in the failure message.</summary>
[Trait("Category", "Unit")]
public sealed class PseudonymizationConfigurationTests
{
    [Fact]
    public void Missing_key_outside_development_or_test_fails_startup()
    {
        using var factory = new Factory("Production", key: null);

        Exception ex = Assert.ThrowsAny<Exception>(() => _ = factory.Services);
        Assert.Contains("PseudonymizationKey", Flatten(ex), StringComparison.Ordinal);
    }

    [Fact]
    public void Key_shorter_than_32_bytes_fails_startup_without_leaking_the_key()
    {
        const string shortKey = "way-too-short-key";
        using var factory = new Factory("Production", shortKey);

        Exception ex = Assert.ThrowsAny<Exception>(() => _ = factory.Services);
        Assert.DoesNotContain(shortKey, Flatten(ex), StringComparison.Ordinal);
    }

    [Fact]
    public void An_explicit_valid_key_lets_the_app_start_and_resolves_the_service()
    {
        using var factory = new Factory("Production", TestPseudonymizer.Key);

        var pseudonymizer = factory.Services.GetRequiredService<IPseudonymizer>();
        Assert.Equal(32, pseudonymizer.SubjectFingerprint("caller-1").Length);
    }

    private static string Flatten(Exception exception)
    {
        var builder = new StringBuilder();
        for (Exception? current = exception; current is not null; current = current.InnerException)
            builder.Append(current.Message).Append('\n');
        return builder.ToString();
    }

    private sealed class Factory(string environment, string? key) : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("Jwt:Authority", "https://sts.coreapi.invalid");
            builder.UseSetting("Jwt:Audience", "coreapi");
            builder.UseSetting("Jwt:Issuer", "https://sts.coreapi.invalid");
            builder.UseSetting("Jwt:DevSigningKeyPath", "");
            builder.UseSetting("DirectoryConnection:Host", "unused.invalid");
            builder.UseSetting("DirectoryConnection:BaseDn", "DC=corp,DC=local");
            builder.UseSetting("DirectoryConnection:UseTls", "true");
            if (key is not null)
                builder.UseSetting("Observability:PseudonymizationKey", key);
        }
    }
}
