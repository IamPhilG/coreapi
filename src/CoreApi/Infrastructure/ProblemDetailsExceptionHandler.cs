using System.DirectoryServices.Protocols;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace CoreApi.Infrastructure;

/// <summary>
/// Maps unhandled exceptions to RFC 7807 ProblemDetails with the right HTTP status.
/// Directory connectivity failures (LdapException, DirectoryOperationException) surface as
/// 503 with a Retry-After header rather than a bare 500 -- the caller should retry, not treat
/// it as a permanent failure. Messages for 503/500 are fixed, generic strings rather than
/// exception.Message, so backend infrastructure details never reach the caller.
/// </summary>
public sealed class ProblemDetailsExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Not Found", exception.Message),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict", exception.Message),
            LdapException or DirectoryOperationException => (
                StatusCodes.Status503ServiceUnavailable,
                "Directory Unavailable",
                "The directory service is temporarily unavailable. Please retry."),
            _ => (StatusCodes.Status500InternalServerError, "Internal Server Error", "An unexpected error occurred."),
        };

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
}
