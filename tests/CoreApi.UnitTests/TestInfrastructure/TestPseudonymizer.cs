using System.Text;
using CoreApi.Infrastructure.Observability;

namespace CoreApi.UnitTests.TestInfrastructure;

/// <summary>A fixed, test-only pseudonymization key and factory, so tests use a deterministic HMAC
/// without depending on configuration.</summary>
public static class TestPseudonymizer
{
    // 44 ASCII bytes -- comfortably above the 32-byte minimum.
    public const string Key = "coreapi-test-pseudonymization-key-0123456789";

    public static IPseudonymizer Create() => new HmacPseudonymizer(Encoding.UTF8.GetBytes(Key));
}
