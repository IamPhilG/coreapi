using System.ComponentModel.DataAnnotations;
using CoreApi.Infrastructure;
using Microsoft.IdentityModel.Tokens;

namespace CoreApi.UnitTests.Infrastructure;

public class JwtOptionsTests
{
    private static List<ValidationResult> Validate(JwtOptions options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results;
    }

    private static JwtOptions Valid() => new()
    {
        Authority = "https://sts.example.com",
        Audience = "coreapi",
        Issuer = "https://sts.example.com"
    };

    [Fact]
    [Trait("Category", "Unit")]
    public void Valid_options_produce_no_errors()
    {
        Assert.Empty(Validate(Valid()));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Empty_Authority_fails_validation()
    {
        var options = Valid();
        options.Authority = "";
        Assert.Contains(Validate(options), r => r.MemberNames.Contains(nameof(JwtOptions.Authority)));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Empty_Audience_fails_validation()
    {
        var options = Valid();
        options.Audience = "";
        Assert.Contains(Validate(options), r => r.MemberNames.Contains(nameof(JwtOptions.Audience)));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Empty_Issuer_fails_validation()
    {
        var options = Valid();
        options.Issuer = "";
        Assert.Contains(Validate(options), r => r.MemberNames.Contains(nameof(JwtOptions.Issuer)));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Default_ValidAlgorithms_is_RS256_only()
    {
        Assert.Equal([SecurityAlgorithms.RsaSha256], new JwtOptions().ValidAlgorithms);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Default_DevSigningKeyPath_is_null()
    {
        Assert.Null(new JwtOptions().DevSigningKeyPath);
    }
}
