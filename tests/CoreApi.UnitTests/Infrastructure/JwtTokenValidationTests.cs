using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;

namespace CoreApi.UnitTests.Infrastructure;

/// <summary>
/// Exercises the same TokenValidationParameters shape Program.cs configures for
/// AddJwtBearer, against tokens minted in-process with a throwaway RSA key -- no live IdP,
/// no HTTP calls. Covers the explicit rejection list from the project's evaluation criteria:
/// expired, wrong audience, wrong issuer, tampered signature, alg: none.
/// </summary>
public class JwtTokenValidationTests
{
    private const string Issuer = "https://sts.test.local";
    private const string Audience = "coreapi";

    private static readonly RSA Rsa = RSA.Create(2048);
    private static readonly SigningCredentials SigningCredentials =
        new(new RsaSecurityKey(Rsa), SecurityAlgorithms.RsaSha256);

    private static TokenValidationParameters ValidationParameters() => new()
    {
        ValidateIssuer = true,
        ValidIssuer = Issuer,
        ValidateAudience = true,
        ValidAudience = Audience,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1),
        ValidateIssuerSigningKey = true,
        ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
        IssuerSigningKey = new RsaSecurityKey(Rsa.ExportParameters(false)),
    };

    private static string MintToken(
        string issuer, string audience, DateTime notBefore, DateTime expires, SigningCredentials? credentials)
    {
        var claims = new[] { new Claim(JwtRegisteredClaimNames.Sub, "test-caller") };
        var token = new JwtSecurityToken(issuer, audience, claims, notBefore, expires, credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static bool TryValidate(string jwt, out Exception? error)
    {
        try
        {
            new JwtSecurityTokenHandler().ValidateToken(jwt, ValidationParameters(), out _);
            error = null;
            return true;
        }
        catch (Exception ex)
        {
            error = ex;
            return false;
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Valid_token_passes_validation()
    {
        string jwt = MintToken(Issuer, Audience, DateTime.UtcNow, DateTime.UtcNow.AddHours(1), SigningCredentials);
        Assert.True(TryValidate(jwt, out _));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Expired_token_is_rejected()
    {
        string jwt = MintToken(Issuer, Audience, DateTime.UtcNow.AddHours(-2), DateTime.UtcNow.AddHours(-1), SigningCredentials);
        Assert.False(TryValidate(jwt, out var error));
        Assert.IsType<SecurityTokenExpiredException>(error);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Wrong_audience_is_rejected()
    {
        string jwt = MintToken(Issuer, "some-other-api", DateTime.UtcNow, DateTime.UtcNow.AddHours(1), SigningCredentials);
        Assert.False(TryValidate(jwt, out var error));
        Assert.IsType<SecurityTokenInvalidAudienceException>(error);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Wrong_issuer_is_rejected()
    {
        string jwt = MintToken("https://not-the-configured-issuer.example", Audience, DateTime.UtcNow, DateTime.UtcNow.AddHours(1), SigningCredentials);
        Assert.False(TryValidate(jwt, out var error));
        Assert.IsType<SecurityTokenInvalidIssuerException>(error);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Tampered_signature_is_rejected()
    {
        string jwt = MintToken(Issuer, Audience, DateTime.UtcNow, DateTime.UtcNow.AddHours(1), SigningCredentials);
        string[] parts = jwt.Split('.');
        char last = parts[2][^1];
        parts[2] = parts[2][..^1] + (last == 'A' ? 'B' : 'A');
        string tampered = string.Join('.', parts);

        Assert.False(TryValidate(tampered, out var error));
        // Which subtype surfaces (SecurityTokenInvalidSignatureException vs.
        // SecurityTokenSignatureKeyNotFoundException) depends on incidental byte content of
        // the corrupted signature and is not stable across runs -- confirmed by observing both
        // while writing this test. Assert the family, not a specific member, to avoid flakiness.
        Assert.IsAssignableFrom<SecurityTokenException>(error);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Unsigned_alg_none_token_is_rejected()
    {
        string jwt = MintToken(Issuer, Audience, DateTime.UtcNow, DateTime.UtcNow.AddHours(1), credentials: null);
        Assert.False(TryValidate(jwt, out var error));
        // Microsoft.IdentityModel refuses unsigned tokens outright when
        // ValidateIssuerSigningKey/RequireSignedTokens are on (the default) -- surfaces as a
        // SecurityTokenValidationException family member, not a signature-specific one, since
        // there is no signature to check in the first place.
        Assert.IsAssignableFrom<SecurityTokenException>(error);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Algorithm_outside_the_allowlist_is_rejected()
    {
        // Same trusted key, but signed with RS384 -- a technically valid, correctly verifiable
        // signature that ValidAlgorithms=[RS256] must still reject on its own merits.
        var rs384Credentials = new SigningCredentials(new RsaSecurityKey(Rsa), SecurityAlgorithms.RsaSha384);
        string jwt = MintToken(Issuer, Audience, DateTime.UtcNow, DateTime.UtcNow.AddHours(1), rs384Credentials);

        Assert.False(TryValidate(jwt, out var error));
        // Observed behavior: ValidAlgorithms filters candidate keys by algorithm before
        // signature matching, so an otherwise-correct signature in a disallowed algorithm
        // surfaces as "no matching key" rather than a distinct algorithm-rejection error.
        Assert.IsAssignableFrom<SecurityTokenException>(error);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Token_signed_by_an_untrusted_key_is_rejected()
    {
        // A different RSA key signs with a valid algorithm shape but isn't in the trusted
        // IssuerSigningKey set -- simulates an untrusted/foreign signer (e.g. an attacker with
        // their own keypair) rather than a tampered signature over the original key.
        using var otherRsa = RSA.Create(2048);
        var otherCredentials = new SigningCredentials(new RsaSecurityKey(otherRsa), SecurityAlgorithms.RsaSha256);
        string jwt = MintToken(Issuer, Audience, DateTime.UtcNow, DateTime.UtcNow.AddHours(1), otherCredentials);

        Assert.False(TryValidate(jwt, out var error));
        Assert.IsType<SecurityTokenSignatureKeyNotFoundException>(error);
    }
}
