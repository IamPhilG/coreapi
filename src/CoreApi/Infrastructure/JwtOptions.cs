using System.ComponentModel.DataAnnotations;
using Microsoft.IdentityModel.Tokens;

namespace CoreApi.Infrastructure;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>
    /// OIDC issuer base URL. In non-Development environments this drives automatic
    /// signing-key discovery via "{Authority}/.well-known/openid-configuration". Always
    /// required (fail-fast) even in Development, where <see cref="DevSigningKeyPath"/>
    /// bypasses the live metadata fetch but the value is still validated as configured.
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public string Authority { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string Audience { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// Explicit signature algorithm allowlist. Defense in depth on top of
    /// RequireSignedTokens=true -- rejects "alg: none" and any algorithm not listed here,
    /// even if a caller's IdP were ever misconfigured to allow it.
    /// </summary>
    public string[] ValidAlgorithms { get; set; } = [SecurityAlgorithms.RsaSha256];

    /// <summary>
    /// Development-only: path to a local RSA public key (PEM) used to validate tokens
    /// instead of fetching signing keys from Authority's OIDC metadata endpoint. Lets
    /// local dev mint and validate tokens without a live IdP. Must never be set outside
    /// Development -- enforced by Program.cs, not by this class.
    /// </summary>
    public string? DevSigningKeyPath { get; set; }
}
