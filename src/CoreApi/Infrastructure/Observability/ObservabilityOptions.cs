using System.Text;

namespace CoreApi.Infrastructure.Observability;

/// <summary>
/// Observability configuration. <see cref="PseudonymizationKey"/> is the HMAC key backing
/// <see cref="IPseudonymizer"/>: stable per environment, sourced from secure configuration
/// (e.g. the ASP.NET Core environment variable <c>Observability__PseudonymizationKey</c> or the
/// platform secret store), never hard-coded and never logged. It is required (at least
/// <see cref="HmacPseudonymizer.MinimumKeyBytes"/> bytes) in every environment except Development
/// and Test.
/// </summary>
public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    public string? PseudonymizationKey { get; set; }

    /// <summary>True when a usable (long-enough) key has been configured.</summary>
    public bool HasValidPseudonymizationKey =>
        !string.IsNullOrEmpty(PseudonymizationKey)
        && Encoding.UTF8.GetByteCount(PseudonymizationKey) >= HmacPseudonymizer.MinimumKeyBytes;
}
