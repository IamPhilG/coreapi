using System.DirectoryServices.Protocols;
using System.Net;
using Microsoft.Extensions.Options;

namespace CoreApi.Infrastructure;

public sealed class LdapDirectoryConnection : IDirectoryConnection, IDisposable
{
    private readonly LdapConnection _connection;
    private readonly TimeSpan _timeout;

    public LdapDirectoryConnection(IOptions<DirectoryConnectionOptions> options)
    {
        var opt = options.Value;
        _timeout = TimeSpan.FromSeconds(opt.TimeoutSeconds);

        var identifier = new LdapDirectoryIdentifier(opt.Host, opt.Port);

        NetworkCredential? credential = null;
        if (!string.IsNullOrEmpty(opt.ServiceAccountUser))
            credential = new NetworkCredential(opt.ServiceAccountUser, opt.ServiceAccountPassword);

        _connection = new LdapConnection(identifier, credential, AuthType.Negotiate);
        _connection.SessionOptions.ProtocolVersion = 3;
        _connection.SessionOptions.SecureSocketLayer = opt.UseTls;
        _connection.SessionOptions.ReferralChasing = ReferralChasingOptions.None;
        _connection.Timeout = _timeout;
        _connection.AutoBind = true;
    }

    public async Task<IReadOnlyList<SearchResultEntry>> SearchAsync(
        string baseDn,
        string filter,
        SearchScope scope,
        string[]? attributes = null,
        DirectoryControl[]? controls = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<SearchResultEntry>();
        var pageControl = new PageResultRequestControl(pageSize: 1000);

        do
        {
            var request = new SearchRequest(baseDn, filter, scope, attributes);
            request.TimeLimit = _timeout;
            request.Controls.Add(pageControl);
            if (controls != null)
                foreach (var c in controls)
                    request.Controls.Add(c);

            var response = (SearchResponse)await SendCoreAsync(request, cancellationToken);

            foreach (SearchResultEntry entry in response.Entries)
                results.Add(entry);

            var pageResponse = response.Controls
                .OfType<PageResultResponseControl>()
                .FirstOrDefault();

            pageControl.Cookie = pageResponse?.Cookie ?? Array.Empty<byte>();
        }
        while (pageControl.Cookie.Length > 0);

        return results;
    }

    public async Task AddAsync(AddRequest request, CancellationToken cancellationToken = default)
        => await SendCoreAsync(request, cancellationToken);

    public async Task ModifyAsync(ModifyRequest request, CancellationToken cancellationToken = default)
        => await SendCoreAsync(request, cancellationToken);

    public async Task DeleteAsync(string distinguishedName, CancellationToken cancellationToken = default)
        => await SendCoreAsync(new DeleteRequest(distinguishedName), cancellationToken);

    public async Task MoveAsync(
        string distinguishedName,
        string? newParentDn,
        string newName,
        CancellationToken cancellationToken = default)
    {
        var request = new ModifyDNRequest(distinguishedName, newParentDn, newName) { DeleteOldRdn = true };
        await SendCoreAsync(request, cancellationToken);
    }

    private Task<DirectoryResponse> SendCoreAsync(
        DirectoryRequest request, CancellationToken cancellationToken)
    {
        return Task.Factory.FromAsync(
            (cb, state) => _connection.BeginSendRequest(
                request, _timeout, PartialResultProcessing.NoPartialResultSupport, cb, state),
            ar => _connection.EndSendRequest(ar),
            state: null);
    }

    public void Dispose() => _connection.Dispose();
}
