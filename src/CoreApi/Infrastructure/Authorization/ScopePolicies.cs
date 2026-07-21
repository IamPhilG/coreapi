using System.Security.Claims;

namespace CoreApi.Infrastructure.Authorization;

/// <summary>
/// OAuth2 scope policy names, following the coreapi.ad.&lt;tier&gt;.&lt;resource&gt;.&lt;verb&gt;
/// convention from the authorization/access model. Only the scopes actually enforced by a
/// controller today are defined here -- new resources/verbs are added incrementally as the
/// specs that need them are built, not enumerated upfront.
/// </summary>
public static class ScopePolicies
{
    public const string UsersRead = "coreapi.ad.t2.users.read";
    public const string UsersCreate = "coreapi.ad.t2.users.create";
    public const string UsersUpdate = "coreapi.ad.t2.users.update";
    public const string UsersDelete = "coreapi.ad.t2.users.delete";
    public const string GroupsRead = "coreapi.ad.t2.groups.read";

    /// <summary>
    /// True if <paramref name="user"/> carries <paramref name="scope"/>. Checks both scope
    /// claim shapes seen across IdPs: "scp" as one claim per value (Okta) and "scope" as a
    /// single space-delimited value (the RFC 9068 JWT access token profile).
    /// </summary>
    public static bool HasScope(ClaimsPrincipal user, string scope) =>
        user.FindAll("scp")
            .Concat(user.FindAll("scope"))
            .SelectMany(claim =>
                claim.Value.Split(
                    ' ',
                    StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Contains(scope, StringComparer.Ordinal);
}
