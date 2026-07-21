using CoreApi.Models;
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

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildGroupMembershipFilter_matches_groups_whose_member_is_the_user_dn()
    {
        string filter = UserService.BuildGroupMembershipFilter("CN=John Smith,OU=Users,DC=corp,DC=local");
        Assert.Equal(
            "(&(objectCategory=group)(member=CN=John Smith,OU=Users,DC=corp,DC=local))", filter);
    }

    [Theory]
    [Trait("Category", "Unit")]
    // A CN can legitimately contain characters that are also LDAP filter metacharacters --
    // without escaping the user DN, a "Smith (contractor)" or a "*" would break out of the filter.
    [InlineData(@"CN=Smith (x),DC=corp,DC=local", @"CN=Smith \28x\29,DC=corp,DC=local")]
    [InlineData(@"CN=a*b,DC=corp,DC=local", @"CN=a\2ab,DC=corp,DC=local")]
    [InlineData(@"CN=a\b,DC=corp,DC=local", @"CN=a\5cb,DC=corp,DC=local")]
    public void BuildGroupMembershipFilter_escapes_ldap_special_characters(string dn, string escaped)
    {
        string filter = UserService.BuildGroupMembershipFilter(dn);
        Assert.Contains($"(member={escaped})", filter);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void BuildGroupMembershipFilter_uses_direct_member_not_matching_rule_in_chain()
    {
        string filter = UserService.BuildGroupMembershipFilter("CN=x,DC=corp,DC=local");

        // 1.2.840.113556.1.4.1941 is LDAP_MATCHING_RULE_IN_CHAIN; "member:" is the extensible
        // match syntax. Direct membership must use neither -- it is a plain (member=<dn>) equality.
        Assert.DoesNotContain("1.2.840.113556.1.4.1941", filter);
        Assert.DoesNotContain("member:", filter);
        Assert.Contains("(member=", filter);
    }

    private static GroupDto Group(string samAccountName, string objectGuid, string? canonicalName = null) => new()
    {
        ObjectGuid = objectGuid,
        SamAccountName = samAccountName,
        DisplayName = samAccountName,
        DistinguishedName = $"CN={samAccountName},OU=Groups,DC=corp,DC=local",
        CanonicalName = canonicalName,
    };

    [Fact]
    [Trait("Category", "Unit")]
    public void OrderAndDedupe_removes_groups_sharing_an_object_guid()
    {
        const string guid = "11111111-1111-1111-1111-111111111111";
        var result = UserService.OrderAndDedupe([Group("Admins", guid), Group("Admins", guid)]);

        Assert.Single(result);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void OrderAndDedupe_orders_by_sam_then_object_guid_tiebreak()
    {
        var beta = Group("beta", "aaaaaaaa-0000-0000-0000-000000000000");
        var alpha = Group("alpha", "ffffffff-0000-0000-0000-000000000000");
        var tieHigh = Group("tie", "20000000-0000-0000-0000-000000000000");
        var tieLow = Group("tie", "10000000-0000-0000-0000-000000000000");

        var result = UserService.OrderAndDedupe([beta, tieHigh, alpha, tieLow]);

        Assert.Equal(["alpha", "beta", "tie", "tie"], result.Select(g => g.SamAccountName));
        // Deterministic tie-break: objectGuid ascending, never the input/native order.
        Assert.Equal("10000000-0000-0000-0000-000000000000", result[2].ObjectGuid);
        Assert.Equal("20000000-0000-0000-0000-000000000000", result[3].ObjectGuid);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void OrderAndDedupe_returns_every_group_when_more_than_100()
    {
        var groups = Enumerable.Range(0, 101)
            .Select(i => Group($"g{i:D4}", Guid.NewGuid().ToString()))
            .ToList();

        var result = UserService.OrderAndDedupe(groups);

        Assert.Equal(101, result.Count);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void OrderAndDedupe_preserves_dn_and_tolerates_absent_canonical_name()
    {
        var result = UserService.OrderAndDedupe([Group("Admins", Guid.NewGuid().ToString(), canonicalName: null)]);

        Assert.Null(result[0].CanonicalName);
        Assert.Equal("CN=Admins,OU=Groups,DC=corp,DC=local", result[0].DistinguishedName);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void OrderAndDedupe_returns_empty_for_no_groups()
    {
        Assert.Empty(UserService.OrderAndDedupe([]));
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
