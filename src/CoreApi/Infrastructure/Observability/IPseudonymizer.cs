namespace CoreApi.Infrastructure.Observability;

/// <summary>
/// Produces stable, keyed, domain-separated pseudonyms for values that must be correlatable in
/// operational logs without appearing in clear.
///
/// This is <b>pseudonymisation, not anonymisation</b>: with the key, or by brute-forcing candidate
/// inputs, a value could still be recovered. The key is stable per environment (so a subject/object
/// maps to the same fingerprint across a deployment) and is held only in the platform secret store,
/// never logged. Rotating the key changes every fingerprint and therefore breaks correlation with
/// historical logs. The exact identity and its controlled retention are the job of the future
/// business audit journal, not of these traces.
/// </summary>
public interface IPseudonymizer
{
    /// <summary>Keyed fingerprint of a caller subject (the <c>sub</c> claim), in the "subject" domain.</summary>
    string SubjectFingerprint(string? value);

    /// <summary>Keyed fingerprint of a directory object (a DN/base DN), in the "object" domain.</summary>
    string ObjectFingerprint(string? value);
}
