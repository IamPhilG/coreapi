using Microsoft.Extensions.Logging;

namespace CoreApi.Infrastructure.Observability;

/// <summary>
/// Stable <see cref="EventId"/>s for CoreApi's operational events. Tests assert on these ids (and
/// structured properties) rather than on message text, so wording can change without breaking
/// alerting or tests.
/// </summary>
public static class ObservabilityEvents
{
    public static readonly EventId ApplicationStarted = new(1000, nameof(ApplicationStarted));
    public static readonly EventId ApplicationStopping = new(1001, nameof(ApplicationStopping));

    public static readonly EventId RequestCompleted = new(1100, nameof(RequestCompleted));

    public static readonly EventId LdapOperationSucceeded = new(1200, nameof(LdapOperationSucceeded));
    public static readonly EventId LdapOperationFailed = new(1201, nameof(LdapOperationFailed));
    public static readonly EventId LdapOperationCancelled = new(1202, nameof(LdapOperationCancelled));

    public static readonly EventId RequestExceptionHandled = new(1300, nameof(RequestExceptionHandled));
    public static readonly EventId DirectoryExceptionHandled = new(1301, nameof(DirectoryExceptionHandled));
    public static readonly EventId UnexpectedExceptionHandled = new(1302, nameof(UnexpectedExceptionHandled));
}
