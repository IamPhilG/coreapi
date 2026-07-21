using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using CoreApi.Infrastructure.Authorization;
using CoreApi.Models;
using CoreApi.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace CoreApi.UnitTests.Controllers;

// Exercises the Spec 4 scope-authorization retrofit (CODE-01) end-to-end through the real
// ASP.NET Core auth pipeline. No AD connectivity involved: IUserService is replaced with a
// stub, so this stays a Unit test per the project's own categorization.
[Trait("Category", "Unit")]
public sealed class UsersControllerAuthorizationTests : IClassFixture<UsersControllerAuthorizationTests.Factory>
{
    private const string Issuer = "https://dev-sts.coreapi.local";
    private const string Audience = "coreapi";

    private readonly Factory _factory;

    public UsersControllerAuthorizationTests(Factory factory) => _factory = factory;

    public static TheoryData<string, string, string, string?> Endpoints => new()
    {
        { "GET", "/v1/users", ScopePolicies.UsersRead, null },
        { "GET", "/v1/users/jsmith", ScopePolicies.UsersRead, null },
        { "GET", "/v1/users/jsmith/groups", ScopePolicies.GroupsRead, null },
        { "POST", "/v1/users", ScopePolicies.UsersCreate, "create" },
        { "PUT", "/v1/users/jsmith", ScopePolicies.UsersUpdate, "update" },
        { "DELETE", "/v1/users/jsmith", ScopePolicies.UsersDelete, null },
    };

    private static HttpContent CreateBody() => JsonContent.Create(new CreateUserRequest
    {
        SamAccountName = "jsmith",
        UserPrincipalName = "jsmith@corp.local",
        OuPath = "OU=Users,DC=corp,DC=local",
    });

    private static HttpContent UpdateBody() => JsonContent.Create(new UpdateUserRequest { DisplayName = "Jane Smith" });

    private static HttpRequestMessage CreateRequest(string method, string path, string? bodyKind)
    {
        HttpContent? content = bodyKind switch
        {
            null => null,
            "create" => CreateBody(),
            "update" => UpdateBody(),
            _ => throw new ArgumentOutOfRangeException(
                nameof(bodyKind), bodyKind, "Unknown request body kind."),
        };

        return new HttpRequestMessage(new HttpMethod(method), path)
        {
            Content = content,
        };
    }

    [Fact]
    public async Task List_without_a_token_returns_401()
    {
        HttpResponseMessage response = await _factory.CreateClient().GetAsync("/v1/users");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Swagger_document_lists_users_endpoints_with_their_required_scope()
    {
        // Regression guard: [ApiExplorerSettings(GroupName = "Users")] makes Swashbuckle's
        // default DocInclusionPredicate drop every action from the "v1" document (it treats
        // GroupName as a document selector, not a display tag) -- Program.cs overrides the
        // predicate to fix this. Also confirms AuthorizeCheckOperationFilter documents the
        // required scope per operation.
        HttpResponseMessage response = await _factory.CreateClient().GetAsync("/swagger/v1/swagger.json");
        string json = await response.Content.ReadAsStringAsync();
        using var document = System.Text.Json.JsonDocument.Parse(json);

        var paths = document.RootElement.GetProperty("paths");
        Assert.True(paths.TryGetProperty("/v1/users", out var usersPath));

        string? getDescription = usersPath.GetProperty("get").GetProperty("description").GetString();
        Assert.Equal($"Requires scope: `{ScopePolicies.UsersRead}`.", getDescription);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Action_with_a_valid_token_missing_the_required_scope_returns_403(
        string method, string path, string requiredScope, string? bodyKind)
    {
        // Holds a real, unrelated scope -- proves the check rejects on identity, not on
        // "no scopes at all". Must not collide with any scope actually required by a row above
        // (groups.read is now a required scope), so use a different-tier scope no endpoint grants.
        string token = _factory.MintToken(Issuer, Audience, "coreapi.ad.t1.users.read");
        var request = CreateRequest(method, path, bodyKind);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await _factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        _ = requiredScope; // documents intent per row; the assertion is the same shape for all rows
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Action_with_the_required_scope_is_not_forbidden(
        string method, string path, string requiredScope, string? bodyKind)
    {
        string token = _factory.MintToken(Issuer, Audience, requiredScope);
        var request = CreateRequest(method, path, bodyKind);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response = await _factory.CreateClient().SendAsync(request);

        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    public sealed class Factory : WebApplicationFactory<Program>
    {
        private readonly RSA _signingKey = RSA.Create(2048);
        private string? _publicKeyPath;

        public string MintToken(string issuer, string audience, params string[] scopes)
        {
            var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, "test-caller") };
            claims.AddRange(scopes.Select(s => new Claim("scp", s)));

            var credentials = new SigningCredentials(new RsaSecurityKey(_signingKey), SecurityAlgorithms.RsaSha256);
            DateTime now = DateTime.UtcNow;
            var token = new JwtSecurityToken(issuer, audience, claims, now, now.AddHours(1), credentials);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            _publicKeyPath = Path.Combine(Path.GetTempPath(), $"coreapi-test-signing-key-{Guid.NewGuid():N}.pem");
            File.WriteAllText(_publicKeyPath, _signingKey.ExportRSAPublicKeyPem());

            // UseSetting (not ConfigureAppConfiguration+AddInMemoryCollection) so these values
            // take precedence over src/CoreApi's own appsettings.Development.json.
            builder.UseEnvironment("Development");
            builder.UseSetting("Jwt:Authority", Issuer);
            builder.UseSetting("Jwt:Audience", Audience);
            builder.UseSetting("Jwt:Issuer", Issuer);
            builder.UseSetting("Jwt:DevSigningKeyPath", _publicKeyPath);
            builder.UseSetting("DirectoryConnection:Host", "unused.invalid");
            builder.UseSetting("DirectoryConnection:BaseDn", "DC=corp,DC=local");
            builder.UseSetting("DirectoryConnection:UseTls", "false");

            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IUserService>();
                services.AddScoped<IUserService, StubUserService>();
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

        private static readonly GroupDto SampleGroup = new()
        {
            ObjectGuid = System.Guid.NewGuid().ToString(),
            SamAccountName = "Admins",
            DisplayName = "Administrators",
            DistinguishedName = "CN=Admins,OU=Groups,DC=corp,DC=local",
            CanonicalName = "corp.local/Groups/Admins",
        };

        public Task<UserDto> GetBySamAccountNameAsync(string samAccountName, CancellationToken cancellationToken = default) =>
            Task.FromResult(Sample);

        public Task<IReadOnlyList<GroupDto>> GetGroupMembershipsAsync(string samAccountName, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<GroupDto>>([SampleGroup]);

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
