using System.Diagnostics;
using System.DirectoryServices.Protocols;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using CoreApi.Infrastructure.Observability;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CoreApi.Infrastructure;

public sealed class LdapDirectoryConnection : IDirectoryConnection, IDisposable
{
    private readonly LdapConnection _connection;
    private readonly TimeSpan _timeout;
    private readonly int _maxSearchResults;
    private readonly ILogger<LdapDirectoryConnection> _logger;
    private readonly IPseudonymizer _pseudonymizer;
    private readonly string _host;
    private readonly string _transport;
    private readonly CancellationTokenSource _shutdownCts = new();
    private readonly SemaphoreSlim _bindLock = new(1, 1);
    private volatile bool _isBound;
    private int _disposed;

    public LdapDirectoryConnection(
        IOptions<DirectoryConnectionOptions> options,
        ILogger<LdapDirectoryConnection> logger,
        IPseudonymizer pseudonymizer)
    {
        _logger = logger;
        _pseudonymizer = pseudonymizer;
        var opt = options.Value;
        _timeout = TimeSpan.FromSeconds(opt.TimeoutSeconds);
        _maxSearchResults = opt.MaxSearchResults;
        _host = opt.Host;
        _transport = opt.UseTls ? "LDAPS" : "LDAP";

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
            string expectedHost = opt.Host;
            _connection.SessionOptions.VerifyServerCertificate = (_, serverCert) =>
            {
                using var cert2 = new X509Certificate2(serverCert);
                using var chain = new X509Chain();
                if (!chain.Build(cert2))
                {
                    _logger.LogWarning(
                        "LDAP TLS certificate rejected for '{Subject}': {Errors}",
                        cert2.Subject,
                        string.Join("; ", chain.ChainStatus.Select(s => s.StatusInformation.Trim())));
                    return false;
                }

                // Chain trust alone doesn't bind the certificate to the host we're actually
                // connecting to -- without this check, any certificate trusted by the machine
                // (e.g. one issued for a different server by the same enterprise CA) would be
                // accepted, allowing a machine-in-the-middle to intercept the service account
                // bind (AuthType.Basic sends credentials once bound).
                if (!CertificateHostnameMatches(cert2, expectedHost))
                {
                    _logger.LogWarning(
                        "LDAP TLS certificate hostname mismatch: expected '{ExpectedHost}', certificate is for '{CertHost}'.",
                        expectedHost, cert2.GetNameInfo(X509NameType.DnsName, forIssuer: false));
                    return false;
                }

                return true;
            };
        }
    }

    /// <summary>
    /// Matches the certificate's DNS name (SAN, falling back to CN per
    /// <see cref="X509Certificate2.GetNameInfo"/>) against the configured host. Exact match
    /// only -- wildcard certificates (e.g. "*.corp.local") aren't handled, since AD DC
    /// certificates issued by an enterprise CA are conventionally per-FQDN, not wildcard.
    /// Exposed internally for unit testing: the callback itself only runs against a live TLS
    /// handshake.
    /// </summary>
    internal static bool CertificateHostnameMatches(X509Certificate2 certificate, string expectedHost) =>
        string.Equals(
            certificate.GetNameInfo(X509NameType.DnsName, forIssuer: false),
            expectedHost,
            StringComparison.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<SearchResultEntry>> SearchAsync(
        string baseDn,
        string filter,
        SearchScope scope,
        string[]? attributes = null,
        DirectoryControl[]? controls = null,
        int? maxResults = null,
        bool enforceResultCeiling = true,
        CancellationToken cancellationToken = default)
    {
        long start = Stopwatch.GetTimestamp();
        string scopeFingerprint = Fingerprint(baseDn);
        var results = new List<SearchResultEntry>();
        int pageSize = maxResults is int m && m < 1000 ? m : 1000;
        int pageCount = 0;
        var pageControl = new PageResultRequestControl(pageSize);

        try
        {
            do
            {
                var request = new SearchRequest(baseDn, filter, scope, attributes);
                request.TimeLimit = _timeout;
                request.Controls.Add(pageControl);
                if (controls != null)
                    foreach (var c in controls)
                        request.Controls.Add(c);

                var raw = await SendCoreAsync(request, cancellationToken);
                pageCount++;
                if (raw is not SearchResponse response)
                    throw new DirectoryOperationException(
                        null, $"Expected SearchResponse but received {raw.GetType().Name}.");

                foreach (SearchResultEntry entry in response.Entries)
                    results.Add(entry);

                if (maxResults is int cap)
                {
                    // Caller asked for a bounded page (e.g. an HTTP list endpoint) -- reaching the
                    // cap is a normal stopping point, not an error. Trim in case the last page
                    // overshot it.
                    if (results.Count >= cap)
                    {
                        if (results.Count > cap)
                            results.RemoveRange(cap, results.Count - cap);
                        break;
                    }
                }
                else if (enforceResultCeiling && results.Count > _maxSearchResults)
                {
                    // No explicit page requested and the ceiling is enforced -- this is a lookup
                    // expected to match a handful of entries at most (e.g. an exact sAMAccountName
                    // search). Matching this many is itself a sign something is wrong, so fail rather
                    // than silently truncate. Operations whose full result set is the contract pass
                    // enforceResultCeiling: false to enumerate every page exhaustively instead.
                    throw new SearchResultsLimitExceededException(
                        $"Search on '{baseDn}' matched more than {_maxSearchResults} entries. " +
                        "Narrow the query (e.g. a more specific base DN or filter) and retry.");
                }

                var pageResponse = response.Controls
                    .OfType<PageResultResponseControl>()
                    .FirstOrDefault();

                if (pageResponse is null && results.Count >= 1000)
                    _logger.LogWarning(
                        "LDAP Search on {LdapHost} (object={ObjectFingerprint}) returned no paging cookie; results may be truncated at the server size limit.",
                        _host, scopeFingerprint);

                pageControl.Cookie = pageResponse?.Cookie ?? Array.Empty<byte>();
            }
            while (pageControl.Cookie.Length > 0);

            LogSuccess("Search", start, scopeFingerprint, resultCount: results.Count, pageCount: pageCount);
            return results;
        }
        catch (OperationCanceledException)
        {
            LogCancelled("Search", start, scopeFingerprint);
            throw;
        }
        catch (LdapException ex)
        {
            LogFailure("Search", start, scopeFingerprint, ex.ErrorCode, ex);
            throw;
        }
        catch (DirectoryOperationException ex)
        {
            LogFailure("Search", start, scopeFingerprint, (int?)ex.Response?.ResultCode, ex);
            throw;
        }
    }

    public Task AddAsync(AddRequest request, CancellationToken cancellationToken = default)
        => ExecuteAsync("Add", request.DistinguishedName, request, cancellationToken);

    public Task ModifyAsync(ModifyRequest request, CancellationToken cancellationToken = default)
        => ExecuteAsync("Modify", request.DistinguishedName, request, cancellationToken);

    public Task DeleteAsync(string distinguishedName, CancellationToken cancellationToken = default)
        => ExecuteAsync("Delete", distinguishedName, new DeleteRequest(distinguishedName), cancellationToken);

    public Task MoveAsync(
        string distinguishedName,
        string? newParentDn,
        string newName,
        CancellationToken cancellationToken = default)
    {
        // newParentDn = null → rename in place (same OU). newParentDn = DN → move, optionally rename.
        var request = new ModifyDNRequest(distinguishedName, newParentDn, newName) { DeleteOldRdn = true };
        return ExecuteAsync("ModifyDN", distinguishedName, request, cancellationToken);
    }

    public async Task<ExtendedResponse> SendExtendedAsync(
        ExtendedRequest request,
        CancellationToken cancellationToken = default)
    {
        var raw = await ExecuteAsync("Extended", distinguishedName: null, request, cancellationToken);
        if (raw is not ExtendedResponse response)
            throw new DirectoryOperationException(
                null, $"Expected ExtendedResponse but received {raw.GetType().Name}.");
        return response;
    }

    // Times a single (non-search) LDAP operation and emits a structured success/failure/cancelled
    // log carrying only an operation category, target host/transport, duration, and (on error)
    // the LDAP result code -- never the filter, the full DN, or any credential.
    private async Task<DirectoryResponse> ExecuteAsync(
        string operation, string? distinguishedName, DirectoryRequest request, CancellationToken cancellationToken)
    {
        long start = Stopwatch.GetTimestamp();
        string fingerprint = Fingerprint(distinguishedName);
        try
        {
            var response = await SendCoreAsync(request, cancellationToken);
            LogSuccess(operation, start, fingerprint);
            return response;
        }
        catch (OperationCanceledException)
        {
            LogCancelled(operation, start, fingerprint);
            throw;
        }
        catch (LdapException ex)
        {
            LogFailure(operation, start, fingerprint, ex.ErrorCode, ex);
            throw;
        }
        catch (DirectoryOperationException ex)
        {
            LogFailure(operation, start, fingerprint, (int?)ex.Response?.ResultCode, ex);
            throw;
        }
    }

    private void LogSuccess(string operation, long start, string objectFingerprint, int? resultCount = null, int? pageCount = null) =>
        _logger.LogInformation(
            ObservabilityEvents.LdapOperationSucceeded,
            "LDAP {LdapOperation} on {LdapHost} ({LdapTransport}) succeeded in {ElapsedMilliseconds:0.0} ms " +
            "(object={ObjectFingerprint}, results={ResultCount}, pages={PageCount})",
            operation, _host, _transport, Stopwatch.GetElapsedTime(start).TotalMilliseconds,
            objectFingerprint, resultCount, pageCount);

    // Directory/LDAP failures are EXPECTED operational errors: the raw exception is never handed to
    // ILogger (no Message, no ToString, no unredacted server diagnostics) -- only its type, the
    // LDAP result code, and a category. Correlation/trace ids ride on the ambient scope.
    private void LogFailure(string operation, long start, string objectFingerprint, int? ldapCode, Exception exception) =>
        _logger.LogWarning(
            ObservabilityEvents.LdapOperationFailed,
            "LDAP {LdapOperation} on {LdapHost} ({LdapTransport}) failed in {ElapsedMilliseconds:0.0} ms " +
            "(object={ObjectFingerprint}, exceptionType={ExceptionType}, errorCategory={ErrorCategory}, ldapCode={LdapCode})",
            operation, _host, _transport, Stopwatch.GetElapsedTime(start).TotalMilliseconds,
            objectFingerprint, exception.GetType().Name, "directory_error", ldapCode);

    private void LogCancelled(string operation, long start, string objectFingerprint) =>
        _logger.LogWarning(
            ObservabilityEvents.LdapOperationCancelled,
            "LDAP {LdapOperation} on {LdapHost} ({LdapTransport}) was cancelled or timed out after {ElapsedMilliseconds:0.0} ms " +
            "(object={ObjectFingerprint}, errorCategory={ErrorCategory})",
            operation, _host, _transport, Stopwatch.GetElapsedTime(start).TotalMilliseconds,
            objectFingerprint, "cancelled");

    // A stable, keyed tag for a DN/base DN so operations on the same object can be correlated in
    // logs without ever recording the directory structure in clear.
    private string Fingerprint(string? distinguishedName) =>
        _pseudonymizer.ObjectFingerprint(distinguishedName);

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

        using var linked = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken, _shutdownCts.Token);

        IAsyncResult? asyncResult = null;
        var task = Task.Factory.FromAsync(
            (cb, state) => asyncResult = _connection.BeginSendRequest(
                request, _timeout, PartialResultProcessing.NoPartialResultSupport, cb, state),
            ar => _connection.EndSendRequest(ar),
            state: null);

        // WaitAsync alone only stops the caller from waiting -- it doesn't tell the native LDAP
        // operation to stop. Without this registration, a cancelled/disposed-away request keeps
        // running against the server until its own timeout, wasting a connection slot.
        // BeginSendRequest above runs synchronously inside FromAsync, so asyncResult is already
        // set by the time this registration can observe an already-cancelled token.
        using var registration = linked.Token.Register(() =>
        {
            if (asyncResult is not null)
                _connection.Abort(asyncResult);
        });

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
