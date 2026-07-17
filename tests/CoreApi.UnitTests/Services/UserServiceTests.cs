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

    private const string BaseDn = "DC=corp,DC=local";

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("DC=corp,DC=local", true)]              // the base DN itself
    [InlineData("OU=Users,DC=corp,DC=local", true)]      // direct child
    [InlineData("OU=IT,OU=Users,DC=corp,DC=local", true)] // nested descendant
    [InlineData("dc=CORP,dc=LOCAL", true)]               // case-insensitive
    public void IsDnWithinBaseDn_accepts_the_base_dn_and_its_descendants(string dn, bool expected)
    {
        Assert.Equal(expected, UserService.IsDnWithinBaseDn(dn, BaseDn));
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("DC=other,DC=local")]                    // unrelated domain
    [InlineData("OU=Users,DC=corp,DC=local,DC=evil")]     // suffix trick: doesn't end with the base DN
    [InlineData("DC=notcorp,DC=local")]                   // similar but not a real match
    public void IsDnWithinBaseDn_rejects_paths_outside_the_configured_scope(string dn)
    {
        // The scenario the missing check let through: a caller-supplied ouPath pointing
        // anywhere the service account can reach, not just the domain this API administers.
        Assert.False(UserService.IsDnWithinBaseDn(dn, BaseDn));
    }
}
