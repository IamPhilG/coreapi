using System.DirectoryServices.Protocols;
using System.Text;
using CoreApi.Infrastructure;
using CoreApi.Infrastructure.Observability;
using CoreApi.IntegrationTests.TestInfrastructure;
using CoreApi.Models;
using CoreApi.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CoreApi.IntegrationTests.Services;

[Collection("AdDc")]
public class UserServiceTests : IDisposable
{
    private readonly LdapDirectoryConnection _connection;
    private readonly UserService _userService;
    private readonly string _usersContainer;
    private readonly string _domain;

    public UserServiceTests(AdDcProvisionerFixture provisioner)
    {
        var directoryOptions = new DirectoryConnectionOptions
        {
            Host = provisioner.ResolvedHost,
            BaseDn = provisioner.ResolvedBaseDn,
            Port = provisioner.ResolvedPort,
            UseTls = provisioner.ResolvedUseTls,
            ServiceAccountUser = provisioner.ResolvedServiceAccountUser,
            ServiceAccountPassword = provisioner.ResolvedServiceAccountPassword,
            TimeoutSeconds = 10,
        };

        _usersContainer = $"CN=Users,{directoryOptions.BaseDn}";
        _domain = string.Join(".", directoryOptions.BaseDn.Split(',').Select(p => p[3..]));
        _connection = new LdapDirectoryConnection(
            Options.Create(directoryOptions),
            NullLogger<LdapDirectoryConnection>.Instance,
            new HmacPseudonymizer(Encoding.UTF8.GetBytes("coreapi-integration-test-pseudonymization-key-1")));
        _userService = new UserService(_connection, Options.Create(directoryOptions));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task CreateGetUpdateDelete_FullLifecycle_AgainstRealDc()
    {
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string samAccountName = $"itest{suffix}";
        var createRequest = new CreateUserRequest
        {
            SamAccountName = samAccountName,
            UserPrincipalName = $"{samAccountName}@{_domain}",
            OuPath = _usersContainer,
            DisplayName = $"Integration Test {suffix}",
            GivenName = "Integration",
            Surname = "Test",
            Department = "QA",
        };

        var created = await _userService.CreateAsync(createRequest);

        try
        {
            Assert.Equal(samAccountName, created.SamAccountName);
            Assert.False(created.Enabled); // always created disabled -- no LDAPS to set a password yet
            Assert.NotEmpty(created.Guid);
            Assert.Equal("QA", created.Department);

            var fetched = await _userService.GetBySamAccountNameAsync(samAccountName);
            Assert.Equal(created.Guid, fetched.Guid);

            var list = await _userService.ListAsync(_usersContainer, pageSize: 100);
            Assert.Contains(list, u => u.SamAccountName == samAccountName);

            var updated = await _userService.UpdateAsync(
                samAccountName, new UpdateUserRequest { Department = "Engineering", JobTitle = "QA Lead" });
            Assert.Equal("Engineering", updated.Department);
            Assert.Equal("QA Lead", updated.JobTitle);
            Assert.Equal(samAccountName, updated.SamAccountName); // identity fields untouched

            await Assert.ThrowsAsync<ConflictException>(() => _userService.CreateAsync(createRequest));
        }
        finally
        {
            await _userService.DeleteAsync(samAccountName);
        }

        await Assert.ThrowsAsync<NotFoundException>(() => _userService.GetBySamAccountNameAsync(samAccountName));
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetBySamAccountNameAsync_unknown_user_throws_NotFoundException()
    {
        await Assert.ThrowsAsync<NotFoundException>(
            () => _userService.GetBySamAccountNameAsync($"does-not-exist-{Guid.NewGuid():N}"));
    }

    // Verifies the direct-membership contract against a real DC:
    //   User -> direct member of Group-A ; Group-A -> member of Group-B
    // GetGroupMemberships(User) must return Group-A and MUST NOT return Group-B (no transitive
    // expansion, no LDAP_MATCHING_RULE_IN_CHAIN).
    [Fact]
    [Trait("Category", "Integration")]
    public async Task GetGroupMembershipsAsync_returns_only_direct_groups_AgainstRealDc()
    {
        string suffix = Guid.NewGuid().ToString("N")[..8];
        string sam = $"itest{suffix}";
        string userDn = $"CN={sam},{_usersContainer}";
        string groupASam = $"grpA{suffix}";
        string groupBSam = $"grpB{suffix}";
        string groupADn = $"CN={groupASam},{_usersContainer}";
        string groupBDn = $"CN={groupBSam},{_usersContainer}";

        await _userService.CreateAsync(new CreateUserRequest
        {
            SamAccountName = sam,
            UserPrincipalName = $"{sam}@{_domain}",
            OuPath = _usersContainer,
        });

        try
        {
            // Group-A has the user as a direct member.
            await _connection.AddAsync(new AddRequest(groupADn,
                new DirectoryAttribute("objectClass", "group"),
                new DirectoryAttribute("sAMAccountName", groupASam),
                new DirectoryAttribute("displayName", "Group A"),
                new DirectoryAttribute("member", userDn)));

            // Group-B has Group-A as a member (nested) -- the user is NOT a direct member of it.
            await _connection.AddAsync(new AddRequest(groupBDn,
                new DirectoryAttribute("objectClass", "group"),
                new DirectoryAttribute("sAMAccountName", groupBSam),
                new DirectoryAttribute("member", groupADn)));

            var groups = await _userService.GetGroupMembershipsAsync(sam);

            Assert.Contains(groups, g => g.SamAccountName == groupASam);
            Assert.DoesNotContain(groups, g => g.SamAccountName == groupBSam);

            var groupA = groups.Single(g => g.SamAccountName == groupASam);
            Assert.NotEmpty(groupA.ObjectGuid);
            Assert.Equal(groupADn, groupA.DistinguishedName);   // DN is exposed, not masked
            Assert.Equal("Group A", groupA.DisplayName);
            Assert.False(string.IsNullOrEmpty(groupA.CanonicalName)); // AD returns canonicalName
        }
        finally
        {
            await _connection.DeleteAsync(groupBDn);
            await _connection.DeleteAsync(groupADn);
            await _userService.DeleteAsync(sam);
        }
    }

    public void Dispose() => _connection.Dispose();
}
