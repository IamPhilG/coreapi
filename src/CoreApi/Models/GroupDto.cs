namespace CoreApi.Models;

/// <summary>
/// An AD group the user is a <em>direct</em> member of -- the group's <c>member</c> attribute
/// contains the user's DN. Nested (transitive) memberships are not expanded and the primary
/// group (primaryGroupID) is not included; see
/// <see cref="CoreApi.Services.IUserService.GetGroupMembershipsAsync"/>.
/// </summary>
public sealed class GroupDto
{
    /// <summary>objectGUID, formatted as a UUID string. Stable across renames/moves.</summary>
    public required string ObjectGuid { get; init; }

    /// <summary>sAMAccountName of the group.</summary>
    public required string SamAccountName { get; init; }

    public string? DisplayName { get; init; }

    /// <summary>
    /// Distinguished name. Standard operational LDAP data -- deliberately not masked; it is a
    /// complement to, never replaced by, <see cref="CanonicalName"/>.
    /// </summary>
    public required string DistinguishedName { get; init; }

    /// <summary>
    /// canonicalName (e.g. "corp.local/Users/Admins"), a constructed attribute AD returns on
    /// request. Complements the DN. Null when the directory does not return it for this entry.
    /// </summary>
    public string? CanonicalName { get; init; }
}
