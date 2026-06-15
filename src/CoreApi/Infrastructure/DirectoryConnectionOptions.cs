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
}
