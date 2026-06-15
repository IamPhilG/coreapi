using System.DirectoryServices.Protocols;

namespace CoreApi.Infrastructure;

public interface IDirectoryConnection
{
    /// <summary>
    /// Executes a paged LDAP search and returns all matching entries.
    /// </summary>
    Task<IReadOnlyList<SearchResultEntry>> SearchAsync(
        string baseDn,
        string filter,
        SearchScope scope,
        string[]? attributes = null,
        DirectoryControl[]? controls = null,
        CancellationToken cancellationToken = default);

    Task AddAsync(AddRequest request, CancellationToken cancellationToken = default);

    Task ModifyAsync(ModifyRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(string distinguishedName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renames or moves an object. Pass <c>null</c> for <paramref name="newParentDn"/> to rename
    /// in place (same parent OU); pass a DN to move the object to a different container.
    /// </summary>
    Task MoveAsync(
        string distinguishedName,
        string? newParentDn,
        string newName,
        CancellationToken cancellationToken = default);
}
