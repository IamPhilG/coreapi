using System.Security.Claims;
using CoreApi.Infrastructure.Authorization;

namespace CoreApi.UnitTests.Infrastructure;

[Trait("Category", "Unit")]
public class ScopePoliciesTests
{
    private const string RequiredScope = ScopePolicies.UsersRead;

    private static ClaimsPrincipal PrincipalWith(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, "TestAuth"));

    [Fact]
    public void HasScope_true_when_scp_claim_matches()
    {
        var user = PrincipalWith(new Claim("scp", RequiredScope));

        Assert.True(ScopePolicies.HasScope(user, RequiredScope));
    }

    [Fact]
    public void HasScope_true_when_one_of_multiple_scp_claims_matches()
    {
        var user = PrincipalWith(
            new Claim("scp", "coreapi.ad.t2.users.create"),
            new Claim("scp", RequiredScope));

        Assert.True(ScopePolicies.HasScope(user, RequiredScope));
    }

    [Fact]
    public void HasScope_true_when_space_delimited_scope_claim_contains_it()
    {
        var user = PrincipalWith(
            new Claim("scope", $"coreapi.ad.t2.users.create {RequiredScope}"));

        Assert.True(ScopePolicies.HasScope(user, RequiredScope));
    }

    [Fact]
    public void HasScope_false_when_no_matching_claim()
    {
        var user = PrincipalWith(new Claim("scp", "coreapi.ad.t2.users.create"));

        Assert.False(ScopePolicies.HasScope(user, RequiredScope));
    }

    [Fact]
    public void HasScope_false_when_no_scope_claims_at_all()
    {
        var user = PrincipalWith(new Claim(ClaimTypes.Name, "someone"));

        Assert.False(ScopePolicies.HasScope(user, RequiredScope));
    }

    [Fact]
    public void HasScope_does_not_match_a_scope_that_is_only_a_substring()
    {
        var user = PrincipalWith(new Claim("scp", RequiredScope + ".extra"));

        Assert.False(ScopePolicies.HasScope(user, RequiredScope));
    }

    [Fact]
    public void HasScope_true_when_space_delimited_scp_claim_contains_it()
    {
        var user = PrincipalWith(
            new Claim("scp", $"coreapi.ad.t2.users.create {RequiredScope}"));

        Assert.True(ScopePolicies.HasScope(user, RequiredScope));
    }

    [Fact]
    public void HasScope_true_when_one_of_multiple_scope_claims_matches()
    {
        var user = PrincipalWith(
            new Claim("scope", "coreapi.ad.t2.users.create"),
            new Claim("scope", RequiredScope));

        Assert.True(ScopePolicies.HasScope(user, RequiredScope));
    }

    [Fact]
    public void HasScope_false_when_scope_targets_another_tier()
    {
        var user = PrincipalWith(new Claim("scp", "coreapi.ad.t1.users.read"));

        Assert.False(ScopePolicies.HasScope(user, RequiredScope));
    }

    [Fact]
    public void HasScope_false_when_scope_uses_the_deprecated_taxonomy()
    {
        var user = PrincipalWith(
            new Claim("scp", "coreapi.data-workload.standard.users.read"));

        Assert.False(ScopePolicies.HasScope(user, RequiredScope));
    }
}
