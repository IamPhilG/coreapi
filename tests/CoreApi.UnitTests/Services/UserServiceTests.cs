using CoreApi.Services;

namespace CoreApi.UnitTests.Services;

/// <summary>
/// SearchResultEntry (System.DirectoryServices.Protocols) has no public constructor, so the
/// LDAP-result-mapping logic itself can only be exercised against a real DC (see the
/// integration tests). What's covered here is the pure logic UserService factors out
/// specifically so it doesn't need one: filter construction and userAccountControl/DN parsing.
/// </summary>
public class UserServiceTests
{
    [Fact]
    [Trait("Category", "Unit")]
    public void BuildSamAccountNameFilter_wraps_value_in_expected_structure()
    {
        string filter = UserService.BuildSamAccountNameFilter("jsmith");
        Assert.Equal("(&(objectClass=user)(objectCategory=person)(sAMAccountName=jsmith))", filter);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("j*smith", @"j\2asmith")]
    [InlineData("j(smith)", @"j\28smith\29")]
    [InlineData(@"j\smith", @"j\5csmith")]
    public void BuildSamAccountNameFilter_escapes_ldap_special_characters(string input, string escaped)
    {
        string filter = UserService.BuildSamAccountNameFilter(input);
        Assert.Contains($"(sAMAccountName={escaped})", filter);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("CN=John Smith,OU=Users,DC=corp,DC=local", "OU=Users,DC=corp,DC=local")]
    [InlineData("CN=Jane,OU=IT,OU=Users,DC=corp,DC=local", "OU=IT,OU=Users,DC=corp,DC=local")]
    public void ExtractOuPath_returns_parent_container(string dn, string expectedOuPath)
    {
        Assert.Equal(expectedOuPath, UserService.ExtractOuPath(dn));
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(512, true)]   // NORMAL_ACCOUNT, enabled
    [InlineData(514, false)]  // NORMAL_ACCOUNT | ACCOUNTDISABLE
    [InlineData(66048, true)] // NORMAL_ACCOUNT | DONT_EXPIRE_PASSWD, enabled
    [InlineData(66050, false)] // NORMAL_ACCOUNT | DONT_EXPIRE_PASSWD | ACCOUNTDISABLE
    public void IsEnabled_reflects_ACCOUNTDISABLE_bit(int userAccountControl, bool expectedEnabled)
    {
        Assert.Equal(expectedEnabled, UserService.IsEnabled(userAccountControl));
    }
}
