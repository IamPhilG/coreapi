using CoreApi.Infrastructure;
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
            Options.Create(directoryOptions), NullLogger<LdapDirectoryConnection>.Instance);
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

            var list = await _userService.ListAsync(_usersContainer);
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

    public void Dispose() => _connection.Dispose();
}
