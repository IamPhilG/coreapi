using System.ComponentModel.DataAnnotations;

namespace CoreApi.Infrastructure;

/// <summary>
/// Configuration for the minimal, single-node request rate limiter (COREAPI-02).
///
/// These defaults are conservative initial parameters, not a definitive production capacity:
/// they exist to fail fast under obvious abuse, not to model real traffic. Tune
/// <see cref="PermitLimit"/> / <see cref="WindowSeconds"/> per deployment once real load is known.
/// </summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>
    /// Explicit on/off switch. When null (the default), the limiter is enabled in every
    /// non-Development environment and disabled in Development, so local Swagger and local
    /// tests are never throttled. Set explicitly to override the per-environment default.
    /// </summary>
    public bool? Enabled { get; set; }

    /// <summary>Requests allowed per partition within one <see cref="WindowSeconds"/> window.</summary>
    [Range(1, int.MaxValue)]
    public int PermitLimit { get; set; } = 100;

    /// <summary>Length of the fixed window, in seconds.</summary>
    [Range(1, 3600)]
    public int WindowSeconds { get; set; } = 10;

    /// <summary>
    /// Requests to queue once the limit is reached. Kept at 0 by default so saturation surfaces
    /// immediately as HTTP 429 rather than being hidden behind a growing backlog.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int QueueLimit { get; set; }
}
