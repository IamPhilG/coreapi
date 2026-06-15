using CoreApi.Infrastructure;

namespace CoreApi.UnitTests.Infrastructure;

public class LdapFilterEncoderTests
{
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("jsmith",           "jsmith")]
    [InlineData("john*doe",         "john\\2adoe")]
    [InlineData("o(neil)",          "o\\28neil\\29")]
    [InlineData("back\\slash",      "back\\5cslash")]
    [InlineData("null\0byte",       "null\\00byte")]
    [InlineData("all*()\\\0chars",  "all\\2a\\28\\29\\5c\\00chars")]
    [InlineData("",                 "")]
    public void Escape_produces_RFC4515_encoded_output(string input, string expected)
        => Assert.Equal(expected, LdapFilterEncoder.Escape(input));

    [Fact]
    [Trait("Category", "Unit")]
    public void Escape_null_returns_null()
        => Assert.Null(LdapFilterEncoder.Escape(null));

    [Fact]
    [Trait("Category", "Unit")]
    public void Escape_plain_string_is_unchanged()
        => Assert.Equal("Domain Users", LdapFilterEncoder.Escape("Domain Users"));

    [Fact]
    [Trait("Category", "Unit")]
    public void Escape_output_is_safe_in_equality_filter()
    {
        var userInput = "*(injected)";
        var escaped = LdapFilterEncoder.Escape(userInput);
        var filter = $"(sAMAccountName={escaped})";
        // Verify no unescaped meta-characters remain outside the attribute/value delimiters
        Assert.Equal("(sAMAccountName=\\2a\\28injected\\29)", filter);
    }
}
