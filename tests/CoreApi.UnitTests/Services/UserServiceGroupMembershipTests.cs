using System.DirectoryServices.Protocols;
using CoreApi.Infrastructure;
using CoreApi.Services;
using Microsoft.Extensions.Options;

namespace CoreApi.UnitTests.Services;

/// <summary>
/// Service-level behaviour of GetGroupMembershipsAsync that doesn't need a DC. SearchResultEntry
/// has no public constructor, so the positive result-mapping paths (multiple/&gt;100 groups,
/// dedupe, order) are covered by <see cref="UserService.OrderAndDedupe"/> unit tests and the
/// deferred integration test; what a fake connection can prove here is the user-resolution and
/// cancellation contract.
/// </summary>
public class UserServiceGroupMembershipTests
{
    private static UserService CreateService(FakeDirectoryConnection connection) =>
        new(connection, Options.Create(new DirectoryConnectionOptions
        {
            Host = "unused.invalid",
            BaseDn = "DC=corp,DC=local",
        }));

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetGroupMembershipsAsync_unknown_user_throws_NotFoundException()
    {
        // The user lookup returns no entry -> 404, before any group search runs.
        var connection = new FakeDirectoryConnection { SearchBehavior = _ => [] };
        var service = CreateService(connection);

        await Assert.ThrowsAsync<NotFoundException>(
            () => service.GetGroupMembershipsAsync("does-not-exist"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GetGroupMembershipsAsync_propagates_cancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        // Proves the caller's token reaches the LDAP layer: the fake honours it.
        var connection = new FakeDirectoryConnection
        {
            SearchBehavior = ct =>
            {
                ct.ThrowIfCancellationRequested();
                return [];
            },
        };
        var service = CreateService(connection);

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.GetGroupMembershipsAsync("jsmith", cts.Token));
    }

    private sealed class FakeDirectoryConnection : IDirectoryConnection
    {
        public Func<CancellationToken, IReadOnlyList<SearchResultEntry>> SearchBehavior { get; set; } = _ => [];

        public Task<IReadOnlyList<SearchResultEntry>> SearchAsync(
            string baseDn, string filter, SearchScope scope, string[]? attributes = null,
            DirectoryControl[]? controls = null, int? maxResults = null,
            bool enforceResultCeiling = true, CancellationToken cancellationToken = default)
            => Task.FromResult(SearchBehavior(cancellationToken));

        public Task AddAsync(AddRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task ModifyAsync(ModifyRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteAsync(string distinguishedName, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task MoveAsync(string distinguishedName, string? newParentDn, string newName, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ExtendedResponse> SendExtendedAsync(ExtendedRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
