namespace CoreApi.Models;

/// <summary>AD user account, mapped from a subset of standard LDAP user attributes.</summary>
public sealed class UserDto
{
    /// <summary>objectGUID, formatted as a UUID string. Stable across renames/moves.</summary>
    public required string Guid { get; init; }

    /// <summary>sAMAccountName. Max 20 chars, unique per domain.</summary>
    public required string SamAccountName { get; init; }

    /// <summary>userPrincipalName, e.g. jsmith@corp.local.</summary>
    public required string UserPrincipalName { get; init; }

    public string? DisplayName { get; init; }
    public string? GivenName { get; init; }
    public string? Surname { get; init; }
    public string? EmailAddress { get; init; }
    public string? JobTitle { get; init; }
    public string? Department { get; init; }

    /// <summary>Distinguished name of the manager's user object, if set.</summary>
    public string? Manager { get; init; }

    public string? Description { get; init; }

    /// <summary>Parent container path, derived from distinguishedName (e.g. "OU=Users,DC=corp,DC=local").</summary>
    public required string OuPath { get; init; }

    /// <summary>Inverse of the userAccountControl ACCOUNTDISABLE bit.</summary>
    public required bool Enabled { get; init; }
}
