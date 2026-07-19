using System.ComponentModel.DataAnnotations;

namespace CoreApi.Infrastructure;

public sealed class DirectoryConnectionOptions
{
    public const string SectionName = "DirectoryConnection";

    [Required(AllowEmptyStrings = false)]
    public string Host { get; set; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; set; } = 636;

    [Required(AllowEmptyStrings = false)]
    public string BaseDn { get; set; } = string.Empty;

    // Defaults to true; override to false only in Development (non-TLS, port 389)
    public bool UseTls { get; set; } = true;

    public string ServiceAccountUser { get; set; } = string.Empty;

    public string ServiceAccountPassword { get; set; } = string.Empty;

    [Range(1, 300)]
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Safety-net cap for SearchAsync calls that don't request an explicit page (see
    /// UsersController's pageSize, which handles bounding real listings). This only guards
    /// lookups expected to match a handful of entries at most -- e.g. an exact sAMAccountName
    /// search, which AD's own uniqueness constraint means should never return more than one.
    /// Matching more than this is itself a sign something is wrong (a caller bypassing the
    /// paged endpoints, or a broken filter), so it's kept small and fails the request rather
    /// than silently returning a partial result set.
    /// </summary>
    [Range(1, 10_000)]
    public int MaxSearchResults { get; set; } = 100;
}
