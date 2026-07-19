using System.DirectoryServices.Protocols;

namespace CoreApi.Infrastructure;

public interface IDirectoryConnection
{
    /// <summary>
    /// Executes a paged LDAP search.
    /// Filter values derived from user input MUST be escaped via <see cref="LdapFilterEncoder.Escape"/>
    /// before being composed into the filter string.
    /// </summary>
    /// <param name="baseDn">Distinguished name to search under.</param>
    /// <param name="filter">LDAP filter string. User-supplied values must already be escaped.</param>
    /// <param name="scope">Search scope (Base, OneLevel, or Subtree).</param>
    /// <param name="attributes">Attributes to return, or null for all attributes.</param>
    /// <param name="controls">Additional LDAP controls to attach to the request.</param>
    /// <param name="maxResults">
    /// Caps how many entries are returned -- once reached, the result is trimmed and returned
    /// as-is (not an error; the caller asked for a page, they got a page). When null, every
    /// matching entry is fetched, bounded only by the safety-net
    /// DirectoryConnection:MaxSearchResults ceiling, which throws
    /// <see cref="SearchResultsLimitExceededException"/> if exceeded -- appropriate for lookups
    /// that should never legitimately return many entries (e.g. an exact sAMAccountName match),
    /// not for listings a caller expects to page through.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<IReadOnlyList<SearchResultEntry>> SearchAsync(
        string baseDn,
        string filter,
        SearchScope scope,
        string[]? attributes = null,
        DirectoryControl[]? controls = null,
        int? maxResults = null,
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

    /// <summary>
    /// Sends an LDAP Extended Operation (e.g. Password Modify, RFC 3062).
    /// </summary>
    Task<ExtendedResponse> SendExtendedAsync(
        ExtendedRequest request,
        CancellationToken cancellationToken = default);
}
