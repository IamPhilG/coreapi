using System.Security.Cryptography;
using System.Text;

namespace CoreApi.Infrastructure.Observability;

/// <summary>
/// HMAC-SHA-256 implementation of <see cref="IPseudonymizer"/>. Domain separation is achieved by
/// prefixing the value with a domain tag ("subject:" / "object:") before the MAC, so the same raw
/// value yields different fingerprints in different domains. Output is the first 16 bytes
/// (128 bits, 32 hex characters) of the MAC. The key is required to be at least 32 bytes.
/// </summary>
public sealed class HmacPseudonymizer : IPseudonymizer
{
    /// <summary>Emitted when there is no value to fingerprint.</summary>
    public const string NoValue = "-";

    /// <summary>Minimum accepted key length, in bytes.</summary>
    public const int MinimumKeyBytes = 32;

    private const int OutputBytes = 16; // 128-bit fingerprint (32 hex chars).

    private readonly byte[] _key;

    public HmacPseudonymizer(byte[] key)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length < MinimumKeyBytes)
            // Deliberately does not include the key material in the message.
            throw new ArgumentException(
                $"Pseudonymization key must be at least {MinimumKeyBytes} bytes.", nameof(key));

        _key = (byte[])key.Clone();
    }

    public string SubjectFingerprint(string? value) => Fingerprint("subject:", value);

    public string ObjectFingerprint(string? value) => Fingerprint("object:", value);

    private string Fingerprint(string domainPrefix, string? value)
    {
        if (string.IsNullOrEmpty(value))
            return NoValue;

        byte[] input = Encoding.UTF8.GetBytes(domainPrefix + value);
        byte[] mac = HMACSHA256.HashData(_key, input);
        return Convert.ToHexString(mac, 0, OutputBytes).ToLowerInvariant();
    }
}
