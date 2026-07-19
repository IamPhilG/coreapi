using CoreApi.Infrastructure;

namespace CoreApi.UnitTests.Infrastructure;

public class LdapDnEncoderTests
{
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("Smith, John", @"Smith\, John")]
    [InlineData("A+B", @"A\+B")]
    [InlineData(@"back\slash", @"back\\slash")]
    [InlineData("quote\"here", "quote\\\"here")]
    [InlineData("a<b>c", @"a\<b\>c")]
    [InlineData("a;b", @"a\;b")]
    [InlineData("a=b", @"a\=b")]
    public void EscapeRdnValue_escapes_special_characters(string input, string expected)
    {
        Assert.Equal(expected, LdapDnEncoder.EscapeRdnValue(input));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void EscapeRdnValue_escapes_leading_hash()
    {
        Assert.Equal(@"\#tag", LdapDnEncoder.EscapeRdnValue("#tag"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void EscapeRdnValue_escapes_leading_and_trailing_space()
    {
        Assert.Equal(@"\ padded\ ", LdapDnEncoder.EscapeRdnValue(" padded "));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void EscapeRdnValue_leaves_ordinary_names_unchanged()
    {
        Assert.Equal("John Smith", LdapDnEncoder.EscapeRdnValue("John Smith"));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void EscapeRdnValue_prevents_breaking_out_into_a_sibling_RDN()
    {
        // The scenario the missing escaping let through: a DisplayName crafted to inject an
        // extra RDN component into the DN being built as $"CN={cn},{ouPath}".
        string malicious = "Evil,OU=Admins";
        string escaped = LdapDnEncoder.EscapeRdnValue(malicious);
        // The comma being escaped is what matters: it can no longer terminate the RDN value
        // and start a sibling "OU=Admins" component. That the '=' is also escaped is incidental
        // (harmless over-escaping, not itself required by RFC 4514).
        Assert.DoesNotContain(",OU=Admins", escaped);
        Assert.Contains(@"Evil\,OU", escaped);
    }
}
