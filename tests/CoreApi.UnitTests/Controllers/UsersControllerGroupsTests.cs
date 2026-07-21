using System.DirectoryServices.Protocols;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using CoreApi.Infrastructure;
using CoreApi.Infrastructure.Authorization;
using CoreApi.Models;
using CoreApi.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace CoreApi.UnitTests.Controllers;

/// <summary>
/// HTTP contract of GET /v1/users/{samAccountName}/groups, proved end-to-end through the real
/// auth pipeline with a controllable IUserService stub -- no DC involved. Covers the response
/// shapes the group-membership endpoint promises: empty, many (&gt;100), not-found, LDAP failure,
/// and the DTO/error hygiene (no BaseDn leak, DN exposed, null canonicalName).
/// </summary>
[Trait("Category", "Unit")]
public sealed class UsersControllerGroupsTests : IClassFixture<UsersControllerGroupsTests.Factory>
{
    private const string Issuer = "https://dev-sts.coreapi.local";
    private const string Audience = "coreapi";
    private const string BaseDn = "DC=corp,DC=local";

    private readonly Factory _factory;

    public UsersControllerGroupsTests(Factory factory) => _factory = factory;

    private HttpClient CreateClient(IUserService stub) =>
        _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IUserService>();
                services.AddScoped(_ => stub);
            })).CreateClient();

    private async Task<HttpResponseMessage> GetGroups(IUserService stub, string sam = "jsmith")
    {
        HttpClient client = CreateClient(stub);
        var request = new HttpRequestMessage(HttpMethod.Get, $"/v1/users/{sam}/groups");
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _factory.MintToken(Issuer, Audience, ScopePolicies.GroupsRead));
        return await client.SendAsync(request);
    }

    private static GroupDto Group(string sam, string? canonicalName = "corp.local/Groups/x") => new()
    {
        ObjectGuid = System.Guid.NewGuid().ToString(),
        SamAccountName = sam,
        DisplayName = sam,
        DistinguishedName = $"CN={sam},OU=Groups,{BaseDn}",
        CanonicalName = canonicalName,
    };

    [Fact]
    public async Task User_with_no_groups_returns_200_and_empty_array()
    {
        HttpResponseMessage response = await GetGroups(new GroupsStub(_ => Task.FromResult<IReadOnlyList<GroupDto>>([])));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(0, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task More_than_100_direct_groups_are_all_returned()
    {
        IReadOnlyList<GroupDto> many = Enumerable.Range(0, 101).Select(i => Group($"g{i:D4}")).ToList();

        HttpResponseMessage response = await GetGroups(new GroupsStub(_ => Task.FromResult(many)));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(101, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task Unknown_user_returns_404_without_leaking_the_base_dn()
    {
        var stub = new GroupsStub(_ => throw new NotFoundException("User 'jsmith' was not found."));

        HttpResponseMessage response = await GetGroups(stub);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain(BaseDn, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Ldap_failure_returns_generic_503_without_leaking_details()
    {
        // The exception message deliberately carries the BaseDn and a backend detail to prove
        // neither reaches the caller.
        var stub = new GroupsStub(_ => throw new LdapException($"bind failed against {BaseDn} (server secret)"));

        HttpResponseMessage response = await GetGroups(stub);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        string body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(BaseDn, body);
        Assert.DoesNotContain("secret", body);
        Assert.Contains("temporarily unavailable", body);
        Assert.True(response.Headers.Contains("Retry-After"));
    }

    [Fact]
    public async Task Group_dto_exposes_distinguished_name_and_serializes_null_canonical_name()
    {
        var stub = new GroupsStub(_ =>
            Task.FromResult<IReadOnlyList<GroupDto>>([Group("Admins", canonicalName: null)]));

        HttpResponseMessage response = await GetGroups(stub);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement group = doc.RootElement[0];
        Assert.False(string.IsNullOrEmpty(group.GetProperty("distinguishedName").GetString()));
        Assert.Equal(JsonValueKind.Null, group.GetProperty("canonicalName").ValueKind);
    }

    [Fact]
    public async Task Swagger_documents_direct_membership_limitations()
    {
        string json = await _factory.CreateClient().GetStringAsync("/swagger/v1/swagger.json");
        using var doc = JsonDocument.Parse(json);
        JsonElement op = doc.RootElement
            .GetProperty("paths")
            .GetProperty("/v1/users/{samAccountName}/groups")
            .GetProperty("get");

        string description = op.GetProperty("description").GetString()!;
        Assert.Contains("direct", description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("transitive", description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("primaryGroupID", description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(ScopePolicies.GroupsRead, description); // scope note still appended
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
            _publicKeyPath = Path.Combine(Path.GetTempPath(), $"coreapi-groups-test-key-{Guid.NewGuid():N}.pem");
            File.WriteAllText(_publicKeyPath, _signingKey.ExportRSAPublicKeyPem());

            builder.UseEnvironment("Development");
            builder.UseSetting("Jwt:Authority", Issuer);
            builder.UseSetting("Jwt:Audience", Audience);
            builder.UseSetting("Jwt:Issuer", Issuer);
            builder.UseSetting("Jwt:DevSigningKeyPath", _publicKeyPath);
            builder.UseSetting("DirectoryConnection:Host", "unused.invalid");
            builder.UseSetting("DirectoryConnection:BaseDn", BaseDn);
            builder.UseSetting("DirectoryConnection:UseTls", "false");
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

    private sealed class GroupsStub(Func<string, Task<IReadOnlyList<GroupDto>>> groups) : IUserService
    {
        public Task<IReadOnlyList<GroupDto>> GetGroupMembershipsAsync(string samAccountName, CancellationToken cancellationToken = default) =>
            groups(samAccountName);

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
