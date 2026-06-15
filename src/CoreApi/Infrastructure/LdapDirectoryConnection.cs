using System.DirectoryServices.Protocols;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoreApi.Infrastructure;

public sealed class LdapDirectoryConnection : IDirectoryConnection, IDisposable
{
    private readonly LdapConnection _connection;
    private readonly TimeSpan _timeout;
    private readonly ILogger<LdapDirectoryConnection> _logger;
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly SemaphoreSlim _bindLock = new(1, 1);
    private volatile bool _isBound;
    private int _disposed;

    public LdapDirectoryConnection(
        IOptions<DirectoryConnectionOptions> options,
        ILogger<LdapDirectoryConnection> logger)
    {
        _logger = logger;
        var opt = options.Value;
        _timeout = TimeSpan.FromSeconds(opt.TimeoutSeconds);

        var identifier = new LdapDirectoryIdentifier(opt.Host, opt.Port);

        // Simple bind (Basic) is required for explicit service-account credentials — works on
        // non-domain-joined hosts (e.g. AWS containers) and must always run over LDAPS.
        // Negotiate is used only when no credentials are configured (integrated/Kerberos auth
        // on domain-joined hosts or keytab environments).
        NetworkCredential? credential = null;
        var authType = AuthType.Negotiate;
        if (!string.IsNullOrEmpty(opt.ServiceAccountUser))
        {
            credential = new NetworkCredential(opt.ServiceAccountUser, opt.ServiceAccountPassword);
            authType = AuthType.Basic;
        }

        _connection = new LdapConnection(identifier, credential, authType);
        _connection.SessionOptions.ProtocolVersion = 3;
        _connection.SessionOptions.SecureSocketLayer = opt.UseTls;
        _connection.SessionOptions.ReferralChasing = ReferralChasingOptions.None;
        _connection.Timeout = _timeout;
        _connection.AutoBind = false; // Bind is performed lazily on first operation via EnsureBoundAsync.

        if (opt.UseTls)
        {
            _connection.SessionOptions.VerifyServerCertificate = (_, serverCert) =>
            {
                using var cert2 = new X509Certificate2(serverCert);
                using var chain = new X509Chain();
                var valid = chain.Build(cert2);
                if (!valid)
                    _logger.LogWarning(
                        "LDAP TLS certificate rejected for '{Subject}': {Errors}",
                        cert2.Subject,
                        string.Join("; ", chain.ChainStatus.Select(s => s.StatusInformation.Trim())));
                return valid;
            };
        }
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

            var raw = await SendCoreAsync(request, cancellationToken);
            if (raw is not SearchResponse response)
                throw new DirectoryOperationException(
                    null, $"Expected SearchResponse but received {raw.GetType().Name}.");

            foreach (SearchResultEntry entry in response.Entries)
                results.Add(entry);

            var pageResponse = response.Controls
                .OfType<PageResultResponseControl>()
                .FirstOrDefault();

            if (pageResponse is null && results.Count >= 1000)
                _logger.LogWarning(
                    "SearchAsync on '{BaseDn}' returned no paging cookie; results may be truncated at the server size limit.",
                    baseDn);

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
        // newParentDn = null → rename in place (same OU). newParentDn = DN → move, optionally rename.
        var request = new ModifyDNRequest(distinguishedName, newParentDn, newName) { DeleteOldRdn = true };
        await SendCoreAsync(request, cancellationToken);
    }

    public async Task<ExtendedResponse> SendExtendedAsync(
        ExtendedRequest request,
        CancellationToken cancellationToken = default)
    {
        var raw = await SendCoreAsync(request, cancellationToken);
        if (raw is not ExtendedResponse response)
            throw new DirectoryOperationException(
                null, $"Expected ExtendedResponse but received {raw.GetType().Name}.");
        return response;
    }

    // Binds on first use; retries on reconnect. Thread-safe via semaphore.
    private async Task EnsureBoundAsync(CancellationToken cancellationToken)
    {
        if (_isBound) return;
        await _bindLock.WaitAsync(cancellationToken);
        try
        {
            if (!_isBound)
            {
                await Task.Run(() => _connection.Bind(), cancellationToken);
                _isBound = true;
            }
        }
        finally
        {
            _bindLock.Release();
        }
    }

    // Uses the APM async pair so thread-pool threads are not blocked during network I/O.
    // Links the caller token with the shutdown token so Dispose() drains in-flight waiters.
    private async Task<DirectoryResponse> SendCoreAsync(
        DirectoryRequest request, CancellationToken cancellationToken)
    {
        await EnsureBoundAsync(cancellationToken);

        var task = Task.Factory.FromAsync(
            (cb, state) => _connection.BeginSendRequest(
                request, _timeout, PartialResultProcessing.NoPartialResultSupport, cb, state),
            ar => _connection.EndSendRequest(ar),
            state: null);

        if (!cancellationToken.CanBeCanceled)
            return await task.WaitAsync(_shutdownCts.Token);

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _shutdownCts.Token);
        return await task.WaitAsync(linked.Token);
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _shutdownCts.Cancel();   // Signal all WaitAsync callers to stop waiting.
        _shutdownCts.Dispose();
        _bindLock.Dispose();
        _connection.Dispose();
    }
}
