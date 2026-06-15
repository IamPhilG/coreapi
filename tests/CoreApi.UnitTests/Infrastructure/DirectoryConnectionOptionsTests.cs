using System.ComponentModel.DataAnnotations;
using CoreApi.Infrastructure;

namespace CoreApi.UnitTests.Infrastructure;

public class DirectoryConnectionOptionsTests
{
    private static List<ValidationResult> Validate(DirectoryConnectionOptions options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Valid_options_produce_no_errors()
    {
        var options = new DirectoryConnectionOptions
        {
            Host = "dc.corp.local",
            BaseDn = "DC=corp,DC=local"
        };
        Assert.Empty(Validate(options));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Empty_Host_fails_validation()
    {
        var options = new DirectoryConnectionOptions { Host = "", BaseDn = "DC=corp,DC=local" };
        Assert.Contains(Validate(options), r => r.MemberNames.Contains(nameof(DirectoryConnectionOptions.Host)));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Empty_BaseDn_fails_validation()
    {
        var options = new DirectoryConnectionOptions { Host = "dc.corp.local", BaseDn = "" };
        Assert.Contains(Validate(options), r => r.MemberNames.Contains(nameof(DirectoryConnectionOptions.BaseDn)));
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(0)]
    [InlineData(65536)]
    public void Invalid_Port_fails_validation(int port)
    {
        var options = new DirectoryConnectionOptions { Host = "dc", BaseDn = "DC=corp,DC=local", Port = port };
        Assert.Contains(Validate(options), r => r.MemberNames.Contains(nameof(DirectoryConnectionOptions.Port)));
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(0)]
    [InlineData(301)]
    public void Invalid_TimeoutSeconds_fails_validation(int seconds)
    {
        var options = new DirectoryConnectionOptions { Host = "dc", BaseDn = "DC=corp,DC=local", TimeoutSeconds = seconds };
        Assert.Contains(Validate(options), r => r.MemberNames.Contains(nameof(DirectoryConnectionOptions.TimeoutSeconds)));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Default_UseTls_is_true()
    {
        Assert.True(new DirectoryConnectionOptions().UseTls);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Default_Port_is_636()
    {
        Assert.Equal(636, new DirectoryConnectionOptions().Port);
    }
}
