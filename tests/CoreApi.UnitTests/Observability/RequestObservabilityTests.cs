using System.DirectoryServices.Protocols;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using CoreApi.Infrastructure.Authorization;
using CoreApi.Infrastructure.Observability;
using CoreApi.Models;
using CoreApi.Services;
using CoreApi.UnitTests.TestInfrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace CoreApi.UnitTests.Observability;

/// <summary>End-to-end structured-logging contract of CoreApi, proved with an in-memory log
/// collector and a stubbed service -- no DC involved.</summary>
[Trait("Category", "Unit")]
public sealed class RequestObservabilityTests
{
    private const string Issuer = "https://dev-sts.coreapi.local";
    private const string Audience = "coreapi";

    private static GroupDto SampleGroup() => new()
    {
        ObjectGuid = System.Guid.NewGuid().ToString(),
        SamAccountName = "Admins",
        DisplayName = "Admins",
        DistinguishedName = "CN=Admins,OU=Groups,DC=corp,DC=local",
        CanonicalName = "corp.local/Groups/Admins",
    };

    [Fact]
    public async Task Response_carries_a_correlation_id_that_is_also_logged()
    {
        using var factory = new Factory { UserServiceStub = Stub(() => [SampleGroup()]) };
        HttpResponseMessage response = await Send(factory);

        Assert.True(response.Headers.TryGetValues(RequestObservabilityHeader, out var values));
        string correlationId = values!.Single();
        Assert.False(string.IsNullOrWhiteSpace(correlationId));
        Assert.Contains(correlationId, factory.Logs.ScopeValues("CorrelationId"));
    }

    [Fact]
    public async Task Authorization_header_and_token_are_never_logged()
    {
        using var factory = new Factory { UserServiceStub = Stub(() => [SampleGroup()]) };
        string token = factory.MintToken(ScopePolicies.GroupsRead);

        await Send(factory, token);

        Assert.False(factory.Logs.AnyTextContains(token));
        Assert.False(factory.Logs.AnyTextContains("Bearer "));
        Assert.False(factory.Logs.AnyTextContains("Authorization"));
    }

    [Fact]
    public async Task Request_log_reports_the_result_count()
    {
        using var factory = new Factory { UserServiceStub = Stub(() => [SampleGroup(), SampleGroup(), SampleGroup()]) };
        await Send(factory);

        Assert.Contains(factory.Logs.Records, r =>
            r.State.Any(kv => kv.Key == "ResultCount" && Equals(kv.Value, 3)));
    }

    [Fact]
    public async Task Ldap_exception_is_sanitized_to_a_generic_503_with_no_sentinels_leaked()
    {
        const string dnSentinel = "CN=Secret User,OU=Privileged,DC=example,DC=com";
        const string filterSentinel = "(sAMAccountName=secret-user)";
        const string passwordSentinel = "LDAP-PASSWORD-SENTINEL";

        using var factory = new Factory
        {
            UserServiceStub = Stub(() => throw new LdapException(49, $"{dnSentinel} {filterSentinel} {passwordSentinel}")),
        };

        HttpResponseMessage response = await Send(factory);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Contains("temporarily unavailable", await response.Content.ReadAsStringAsync());

        // None of the sentinels leak anywhere into the captured logs.
        Assert.False(factory.Logs.AnyTextContains(dnSentinel));
        Assert.False(factory.Logs.AnyTextContains(filterSentinel));
        Assert.False(factory.Logs.AnyTextContains(passwordSentinel));

        // But the useful category, type and event id are retained -- and the raw exception is not attached.
        LogRecord failure = Assert.Single(
            factory.Logs.Records, r => r.EventId == ObservabilityEvents.DirectoryExceptionHandled);
        Assert.Equal(LogLevel.Warning, failure.Level);
        Assert.Equal("directory_unavailable", (string?)failure.State.Single(kv => kv.Key == "ErrorCategory").Value);
        Assert.Equal("LdapException", (string?)failure.State.Single(kv => kv.Key == "ExceptionType").Value);
        Assert.Null(failure.Exception);
    }

    [Fact]
    public async Task Unexpected_exception_is_sanitized_no_raw_exception_or_sentinels_leaked()
    {
        const string dnSentinel = "CN=Unexpected Secret,OU=Tier0,DC=example,DC=com";
        const string filterSentinel = "(sAMAccountName=unexpected-secret)";
        const string passwordSentinel = "UNEXPECTED-PASSWORD-SENTINEL";

        using var factory = new Factory
        {
            UserServiceStub = Stub(() => throw new InvalidOperationException($"{dnSentinel} {filterSentinel} {passwordSentinel}")),
        };

        HttpResponseMessage response = await Send(factory);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);

        // No sentinel from the exception message leaks anywhere in the logs...
        Assert.False(factory.Logs.AnyTextContains(dnSentinel));
        Assert.False(factory.Logs.AnyTextContains(filterSentinel));
        Assert.False(factory.Logs.AnyTextContains(passwordSentinel));

        LogRecord failure = Assert.Single(
            factory.Logs.Records, r => r.EventId == ObservabilityEvents.UnexpectedExceptionHandled);
        Assert.Equal(LogLevel.Error, failure.Level);

        // ...no Exception object is attached to the record...
        Assert.Null(failure.Exception);

        // ...the useful structured properties are present...
        Assert.Equal("unexpected", (string?)failure.State.Single(kv => kv.Key == "ErrorCategory").Value);
        Assert.Equal("InvalidOperationException", (string?)failure.State.Single(kv => kv.Key == "ExceptionType").Value);
        Assert.Contains(failure.State, kv => kv.Key == "TraceId");

        // ...and a sanitized stack trace keeps at least a diagnostic frame (never the message).
        string? stack = (string?)failure.State.Single(kv => kv.Key == "SanitizedStackTrace").Value;
        Assert.False(string.IsNullOrEmpty(stack));
        Assert.Contains("at ", stack);
        Assert.DoesNotContain(passwordSentinel, stack);
    }

    [Fact]
    public async Task Pseudonymization_key_is_never_written_to_the_logs()
    {
        using var factory = new Factory { UserServiceStub = Stub(() => [SampleGroup()]) };
        await Send(factory);

        Assert.False(factory.Logs.AnyTextContains(TestPseudonymizer.Key));
    }

    [Fact]
    public async Task Subject_is_pseudonymised_never_logged_in_clear()
    {
        const string rawSubject = "philippe.secret@example.test";
        using var factory = new Factory { UserServiceStub = Stub(() => [SampleGroup()]) };
        string token = factory.MintTokenForSubject(rawSubject, ScopePolicies.GroupsRead);

        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/users/jsmith/groups");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        await factory.CreateClient().SendAsync(request);

        LogRecord completed = Assert.Single(
            factory.Logs.Records, r => r.EventId == ObservabilityEvents.RequestCompleted);

        Assert.Equal(true, completed.State.Single(kv => kv.Key == "Authenticated").Value);
        string fingerprint = (string)completed.State.Single(kv => kv.Key == "SubjectFingerprint").Value!;
        Assert.NotEqual("-", fingerprint);
        Assert.NotEqual(rawSubject, fingerprint);
        Assert.False(factory.Logs.AnyTextContains(rawSubject));
    }

    [Fact]
    public async Task Configuration_secret_values_are_never_logged()
    {
        const string secret = "S3cretSentinel-DoNotLog!";
        using var factory = new Factory(new()
        {
            ["DirectoryConnection:ServiceAccountUser"] = "svc-coreapi",
            ["DirectoryConnection:ServiceAccountPassword"] = secret,
        })
        {
            UserServiceStub = Stub(() => [SampleGroup()]),
        };

        await Send(factory);

        Assert.False(factory.Logs.AnyTextContains(secret));
    }

    private const string RequestObservabilityHeader = "X-Correlation-ID";

    private async Task<HttpResponseMessage> Send(Factory factory, string? token = null)
    {
        HttpClient client = factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/v1/users/jsmith/groups");
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", token ?? factory.MintToken(ScopePolicies.GroupsRead));
        return await client.SendAsync(request);
    }

    private static IUserService Stub(Func<IReadOnlyList<GroupDto>> groups) =>
        new StubUserService(() => Task.FromResult(groups()));

    private sealed class Factory(Dictionary<string, string?>? extraSettings = null) : WebApplicationFactory<Program>
    {
        private readonly Dictionary<string, string?> _extraSettings = extraSettings ?? [];
        private readonly RSA _signingKey = RSA.Create(2048);
        private string? _publicKeyPath;

        public InMemoryLogCollector Logs { get; } = new();
        public IUserService? UserServiceStub { get; init; }

        public string MintToken(params string[] scopes) => MintTokenForSubject("test-caller", scopes);

        public string MintTokenForSubject(string subject, params string[] scopes)
        {
            var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, subject) };
            claims.AddRange(scopes.Select(s => new Claim("scp", s)));
            var credentials = new SigningCredentials(new RsaSecurityKey(_signingKey), SecurityAlgorithms.RsaSha256);
            DateTime now = DateTime.UtcNow;
            var token = new JwtSecurityToken(Issuer, Audience, claims, now, now.AddHours(1), credentials);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            _publicKeyPath = Path.Combine(Path.GetTempPath(), $"coreapi-obs-test-key-{Guid.NewGuid():N}.pem");
            File.WriteAllText(_publicKeyPath, _signingKey.ExportRSAPublicKeyPem());

            builder.UseEnvironment("Development");
            builder.UseSetting("Jwt:Authority", Issuer);
            builder.UseSetting("Jwt:Audience", Audience);
            builder.UseSetting("Jwt:Issuer", Issuer);
            builder.UseSetting("Jwt:DevSigningKeyPath", _publicKeyPath);
            builder.UseSetting("DirectoryConnection:Host", "unused.invalid");
            builder.UseSetting("DirectoryConnection:BaseDn", "DC=corp,DC=local");
            builder.UseSetting("DirectoryConnection:UseTls", "false");
            builder.UseSetting("Observability:PseudonymizationKey", TestPseudonymizer.Key);
            foreach ((string key, string? value) in _extraSettings)
                builder.UseSetting(key, value);

            builder.ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.SetMinimumLevel(LogLevel.Debug);
                logging.AddProvider(Logs);
            });

            builder.ConfigureTestServices(services =>
            {
                if (UserServiceStub is not null)
                {
                    services.RemoveAll<IUserService>();
                    services.AddScoped(_ => UserServiceStub);
                }
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!disposing)
                return;

            _signingKey.Dispose();
            if (_publicKeyPath is not null && File.Exists(_publicKeyPath))
                File.Delete(_publicKeyPath);
        }
    }

    private sealed class StubUserService(Func<Task<IReadOnlyList<GroupDto>>> groups) : IUserService
    {
        public Task<IReadOnlyList<GroupDto>> GetGroupMembershipsAsync(string samAccountName, CancellationToken cancellationToken = default) =>
            groups();

        public Task<UserDto> GetBySamAccountNameAsync(string samAccountName, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<UserDto>> ListAsync(string? ouPath, int pageSize, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<UserDto> UpdateAsync(string samAccountName, UpdateUserRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DeleteAsync(string samAccountName, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
