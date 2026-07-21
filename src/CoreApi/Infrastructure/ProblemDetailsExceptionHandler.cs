using System.Diagnostics;
using System.DirectoryServices.Protocols;
using CoreApi.Infrastructure.Observability;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CoreApi.Infrastructure;

/// <summary>
/// Maps unhandled exceptions to RFC 7807 ProblemDetails with the right HTTP status, and logs the
/// internal exception (which is never sent to the client) with the request's trace id and a
/// stable error category. Expected/operational errors log at Warning; unexpected ones at Error.
/// Directory connectivity failures (LdapException, DirectoryOperationException) surface as 503
/// with a Retry-After header rather than a bare 500. Messages for 503/500 are fixed, generic
/// strings rather than exception.Message, so backend infrastructure details never reach the caller.
/// </summary>
public sealed class ProblemDetailsExceptionHandler(ILogger<ProblemDetailsExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail, category, level, eventId) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Not Found", exception.Message, "not_found", LogLevel.Warning, ObservabilityEvents.RequestExceptionHandled),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict", exception.Message, "conflict", LogLevel.Warning, ObservabilityEvents.RequestExceptionHandled),
            InvalidRequestException => (StatusCodes.Status400BadRequest, "Bad Request", exception.Message, "invalid_request", LogLevel.Warning, ObservabilityEvents.RequestExceptionHandled),
            SearchResultsLimitExceededException => (StatusCodes.Status400BadRequest, "Bad Request", exception.Message, "search_limit_exceeded", LogLevel.Warning, ObservabilityEvents.RequestExceptionHandled),
            LdapException or DirectoryOperationException => (
                StatusCodes.Status503ServiceUnavailable,
                "Directory Unavailable",
                "The directory service is temporarily unavailable. Please retry.",
                "directory_unavailable",
                LogLevel.Warning,
                ObservabilityEvents.DirectoryExceptionHandled),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error", "An unexpected error occurred.", "unexpected", LogLevel.Error, ObservabilityEvents.UnexpectedExceptionHandled),
        };

        string traceId = Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;
        httpContext.Items[RequestObservabilityMiddleware.ErrorCategoryItemKey] = category;

        bool unexpected = status == StatusCodes.Status500InternalServerError;

        // The exception object is NEVER attached to the logger (no overload takes it), so its
        // Message/ToString and any server diagnostics can never be rendered by a sink. Expected
        // errors log only type/category/status/trace. A truly unexpected error additionally logs a
        // SANITIZED stack trace (frames only, from Exception.StackTrace -- never Message/ToString)
        // plus the bare type names of its inner exceptions, for diagnosis. The public response
        // stays generic.
        string? sanitizedStackTrace = unexpected ? exception.StackTrace : null;
        string? innerExceptionTypes = unexpected ? InnerExceptionTypeNames(exception) : null;

        logger.Log(level, eventId,
            "Request failed with {ErrorCategory} ({ExceptionType}) -> {StatusCode} " +
            "(traceId={TraceId}, innerTypes={InnerExceptionTypes}, stack={SanitizedStackTrace})",
            category, exception.GetType().Name, status, traceId, innerExceptionTypes, sanitizedStackTrace);

        if (status == StatusCodes.Status503ServiceUnavailable)
            httpContext.Response.Headers.RetryAfter = "30";

        httpContext.Response.StatusCode = status;
        await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Instance = httpContext.Request.Path,
        }, cancellationToken);

        return true;
    }

    // A bounded chain of inner-exception TYPE names only -- never their messages -- for diagnosis.
    private static string? InnerExceptionTypeNames(Exception exception)
    {
        const int maxDepth = 5;
        var names = new List<string>();
        for (Exception? inner = exception.InnerException; inner is not null && names.Count < maxDepth; inner = inner.InnerException)
            names.Add(inner.GetType().Name);
        return names.Count == 0 ? null : string.Join(" -> ", names);
    }
}
