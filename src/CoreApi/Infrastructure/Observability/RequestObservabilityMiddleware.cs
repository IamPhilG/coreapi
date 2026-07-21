using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace CoreApi.Infrastructure.Observability;

/// <summary>
/// Establishes a correlation id for every request, echoes it back on the response, and emits a
/// single structured "request completed" log carrying the fields operators actually need
/// (method, endpoint, status, duration, subject, required policy, result count, error category)
/// -- never the Authorization header, token, or body. The correlation id is pushed as a logging
/// scope so every downstream log (LDAP, exceptions) is correlated automatically.
/// </summary>
public sealed class RequestObservabilityMiddleware(
    RequestDelegate next,
    ILogger<RequestObservabilityMiddleware> logger,
    IPseudonymizer pseudonymizer)
{
    public const string CorrelationIdHeader = "X-Correlation-ID";
    public const string CorrelationIdItemKey = "coreapi.correlation_id";
    public const string ResultCountItemKey = "coreapi.result_count";
    public const string ErrorCategoryItemKey = "coreapi.error_category";

    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId = ResolveCorrelationId(context);
        string traceId = Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

        context.Items[CorrelationIdItemKey] = correlationId;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[CorrelationIdHeader] = correlationId;
            return Task.CompletedTask;
        });

        using IDisposable? scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId,
            ["TraceId"] = traceId,
        });

        long start = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
        }
        finally
        {
            LogCompleted(context, Stopwatch.GetElapsedTime(start).TotalMilliseconds);
        }
    }

    private void LogCompleted(HttpContext context, double elapsedMs)
    {
        Endpoint? endpoint = context.GetEndpoint();

        // The route pattern (e.g. "/v1/users/{samAccountName}/groups"), not the concrete path, so
        // caller-supplied identifiers don't land in operational logs.
        string endpointLabel = (endpoint as RouteEndpoint)?.RoutePattern.RawText ?? "(no endpoint)";

        string? requiredPolicy = endpoint?.Metadata
            .GetOrderedMetadata<IAuthorizeData>()
            .Select(a => a.Policy)
            .FirstOrDefault(p => !string.IsNullOrEmpty(p));

        bool authenticated = context.User.Identity?.IsAuthenticated == true;
        // Pseudonymised, NOT anonymised: SubjectFingerprint is a keyed HMAC of the raw `sub` claim
        // -- enough to correlate a caller's requests in operational traces without recording the
        // identity itself. The exact subject (and its controlled retention) is the job of the
        // future business audit journal, not of these logs.
        string subjectFingerprint =
            pseudonymizer.SubjectFingerprint(authenticated ? context.User.FindFirst("sub")?.Value : null);

        int? resultCount = context.Items[ResultCountItemKey] as int?;
        string? errorCategory = context.Items[ErrorCategoryItemKey] as string;

        logger.LogInformation(
            ObservabilityEvents.RequestCompleted,
            "HTTP {HttpMethod} {Endpoint} responded {StatusCode} in {ElapsedMilliseconds:0.0} ms " +
            "(authenticated={Authenticated}, subject={SubjectFingerprint}, policy={RequiredPolicy}, results={ResultCount}, error={ErrorCategory})",
            context.Request.Method,
            endpointLabel,
            context.Response.StatusCode,
            elapsedMs,
            authenticated,
            subjectFingerprint,
            requiredPolicy ?? "-",
            resultCount,
            errorCategory ?? "-");
    }

    // Accepts a caller-supplied correlation id only if it's short and alphanumeric-ish, so a
    // hostile header value can't forge log lines or inject control characters; otherwise falls
    // back to the ambient trace id.
    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var header))
        {
            string candidate = header.ToString();
            if (candidate.Length is > 0 and <= 128 && candidate.All(IsSafe))
                return candidate;
        }

        return Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;
    }

    private static bool IsSafe(char c) => char.IsLetterOrDigit(c) || c is '-' or '_';
}
