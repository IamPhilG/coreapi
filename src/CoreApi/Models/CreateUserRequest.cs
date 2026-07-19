using System.ComponentModel.DataAnnotations;

namespace CoreApi.Models;

/// <summary>
/// Request body to create a new AD user account. The account is always created disabled
/// (userAccountControl ACCOUNTDISABLE) -- setting an initial password requires LDAPS, which
/// isn't wired up yet. Enabling the account is a separate, currently unimplemented, follow-up.
/// </summary>
public sealed class CreateUserRequest
{
    /// <summary>sAMAccountName. Max 20 chars, unique per domain.</summary>
    [Required(AllowEmptyStrings = false)]
    [StringLength(20)]
    public required string SamAccountName { get; init; }

    /// <summary>userPrincipalName, e.g. jsmith@corp.local.</summary>
    [Required(AllowEmptyStrings = false)]
    public required string UserPrincipalName { get; init; }

    /// <summary>
    /// Distinguished name of the container to create the user in, e.g. "OU=Users,DC=corp,DC=local".
    /// </summary>
    [Required(AllowEmptyStrings = false)]
    public required string OuPath { get; init; }

    public string? DisplayName { get; init; }
    public string? GivenName { get; init; }
    public string? Surname { get; init; }
    public string? EmailAddress { get; init; }
    public string? JobTitle { get; init; }
    public string? Department { get; init; }

    /// <summary>Distinguished name of the manager's user object, if set.</summary>
    public string? Manager { get; init; }

    public string? Description { get; init; }
}
