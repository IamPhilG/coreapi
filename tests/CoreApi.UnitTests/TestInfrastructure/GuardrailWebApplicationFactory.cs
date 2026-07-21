using CoreApi.Models;
using CoreApi.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CoreApi.UnitTests.TestInfrastructure;

/// <summary>
/// Boots the real application through its full HTTP pipeline in a chosen environment, with just
/// enough valid configuration to satisfy fail-fast options validation and with the AD-backed
/// <see cref="IUserService"/> replaced by an in-memory stub. This is the production-guardrail
/// (COREAPI-02) counterpart to the auth tests' factory: it takes no dependency on AWS or a Domain
/// Controller, so it runs anywhere the unit tests run (locally and in CI).
/// </summary>
internal sealed class GuardrailWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _environment;
    private readonly IReadOnlyDictionary<string, string?> _settings;

    public GuardrailWebApplicationFactory(
        string environment, IReadOnlyDictionary<string, string?>? settings = null)
    {
        _environment = environment;
        _settings = settings ?? new Dictionary<string, string?>();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);

        // Minimal valid configuration so ValidateOnStart passes in any environment.
        // UseSetting (host configuration) outranks src/CoreApi's appsettings.*.json.
        builder.UseSetting("Jwt:Authority", "https://sts.coreapi.invalid");
        builder.UseSetting("Jwt:Audience", "coreapi");
        builder.UseSetting("Jwt:Issuer", "https://sts.coreapi.invalid");
        // Empty is valid in every environment and keeps startup off the dev signing-key file,
        // which is gitignored and may be absent.
        builder.UseSetting("Jwt:DevSigningKeyPath", "");
        builder.UseSetting("DirectoryConnection:Host", "unused.invalid");
        builder.UseSetting("DirectoryConnection:BaseDn", "DC=corp,DC=local");
        builder.UseSetting("DirectoryConnection:UseTls", "true");
        // Required outside Development/Test; set here so the factory boots in any environment.
        builder.UseSetting("Observability:PseudonymizationKey", TestPseudonymizer.Key);

        foreach ((string key, string? value) in _settings)
            builder.UseSetting(key, value);

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IUserService>();
            services.AddScoped<IUserService, StubUserService>();
        });
    }

    private sealed class StubUserService : IUserService
    {
        private static readonly UserDto Sample = new()
        {
            Guid = System.Guid.NewGuid().ToString(),
            SamAccountName = "jsmith",
            UserPrincipalName = "jsmith@corp.local",
            OuPath = "OU=Users,DC=corp,DC=local",
            Enabled = true,
        };

        public Task<UserDto> GetBySamAccountNameAsync(string samAccountName, CancellationToken cancellationToken = default) =>
            Task.FromResult(Sample);

        public Task<IReadOnlyList<GroupDto>> GetGroupMembershipsAsync(string samAccountName, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<GroupDto>>([]);

        public Task<IReadOnlyList<UserDto>> ListAsync(string? ouPath, int pageSize, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<UserDto>>([Sample]);

        public Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Sample);

        public Task<UserDto> UpdateAsync(string samAccountName, UpdateUserRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Sample);

        public Task DeleteAsync(string samAccountName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
