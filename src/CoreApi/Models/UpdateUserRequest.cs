namespace CoreApi.Models;

/// <summary>
/// Request body to update an existing AD user's attributes. Identity fields
/// (samAccountName, userPrincipalName) and placement (ouPath) are not updatable here --
/// renaming/moving an object is an LDAP ModifyDN operation, a separate, currently
/// unimplemented, follow-up action. Fields left null are not modified.
/// </summary>
public sealed class UpdateUserRequest
{
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
