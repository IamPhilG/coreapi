using CoreApi.Models;

namespace CoreApi.Services;

public interface IUserService
{
    /// <summary>Throws <see cref="CoreApi.Infrastructure.NotFoundException"/> if no matching user exists.</summary>
    Task<UserDto> GetBySamAccountNameAsync(string samAccountName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the groups the user is a <em>direct</em> member of (a group whose <c>member</c>
    /// attribute contains the user's DN). Nested/transitive memberships are not expanded -- a
    /// group that is itself a member of another group is returned, but that outer group is not.
    /// The primary group (primaryGroupID, typically "Domain Users") is out of scope for this
    /// increment and is not included. Throws
    /// <see cref="CoreApi.Infrastructure.NotFoundException"/> if no matching user exists.
    /// </summary>
    Task<IReadOnlyList<GroupDto>> GetGroupMembershipsAsync(string samAccountName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists users under <paramref name="ouPath"/>, or the configured directory base DN if
    /// null. Returns at most <paramref name="pageSize"/> entries -- there is no continuation
    /// token yet, so a domain with more matches than that requires narrowing <paramref
    /// name="ouPath"/>, not paging through the rest.
    /// </summary>
    Task<IReadOnlyList<UserDto>> ListAsync(string? ouPath, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>Throws <see cref="CoreApi.Infrastructure.ConflictException"/> if the sAMAccountName is already in use.</summary>
    Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>Throws <see cref="CoreApi.Infrastructure.NotFoundException"/> if no matching user exists.</summary>
    Task<UserDto> UpdateAsync(string samAccountName, UpdateUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>Throws <see cref="CoreApi.Infrastructure.NotFoundException"/> if no matching user exists.</summary>
    Task DeleteAsync(string samAccountName, CancellationToken cancellationToken = default);
}
