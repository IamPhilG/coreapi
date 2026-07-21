using System.Text;
using CoreApi.Infrastructure.Observability;
using CoreApi.UnitTests.TestInfrastructure;

namespace CoreApi.UnitTests.Observability;

[Trait("Category", "Unit")]
public class PseudonymizerTests
{
    private static readonly IPseudonymizer Pseudonymizer = TestPseudonymizer.Create();

    [Fact]
    public void Same_subject_and_key_yield_the_same_fingerprint()
    {
        Assert.Equal(Pseudonymizer.SubjectFingerprint("caller-1"), Pseudonymizer.SubjectFingerprint("caller-1"));
    }

    [Fact]
    public void Different_subjects_yield_different_fingerprints()
    {
        Assert.NotEqual(Pseudonymizer.SubjectFingerprint("caller-1"), Pseudonymizer.SubjectFingerprint("caller-2"));
    }

    [Fact]
    public void Subject_and_object_domains_are_separated_for_the_same_value()
    {
        Assert.NotEqual(Pseudonymizer.SubjectFingerprint("same-value"), Pseudonymizer.ObjectFingerprint("same-value"));
    }

    [Theory]
    [InlineData("a")]
    [InlineData("philippe.secret@example.test")]
    [InlineData("CN=Someone,OU=Users,DC=corp,DC=local")]
    public void Fingerprint_is_exactly_32_lowercase_hex_characters(string value)
    {
        foreach (string fingerprint in new[] { Pseudonymizer.SubjectFingerprint(value), Pseudonymizer.ObjectFingerprint(value) })
        {
            Assert.Equal(32, fingerprint.Length);
            Assert.Matches("^[0-9a-f]{32}$", fingerprint);
        }
    }

    [Fact]
    public void Null_or_empty_input_yields_the_no_value_sentinel()
    {
        Assert.Equal(HmacPseudonymizer.NoValue, Pseudonymizer.SubjectFingerprint(null));
        Assert.Equal(HmacPseudonymizer.NoValue, Pseudonymizer.ObjectFingerprint(string.Empty));
    }

    [Fact]
    public void Different_keys_yield_different_fingerprints_for_the_same_subject()
    {
        var other = new HmacPseudonymizer(Encoding.UTF8.GetBytes("another-distinct-test-key-abcdefghijklmnopq"));
        Assert.NotEqual(Pseudonymizer.SubjectFingerprint("caller-1"), other.SubjectFingerprint("caller-1"));
    }

    [Fact]
    public void A_key_shorter_than_32_bytes_is_rejected()
    {
        Assert.Throws<ArgumentException>(() => new HmacPseudonymizer(Encoding.UTF8.GetBytes("short-key")));
    }
}
