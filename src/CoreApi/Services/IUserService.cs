using CoreApi.Models;

namespace CoreApi.Services;

public interface IUserService
{
    /// <summary>Throws <see cref="CoreApi.Infrastructure.NotFoundException"/> if no matching user exists.</summary>
    Task<UserDto> GetBySamAccountNameAsync(string samAccountName, CancellationToken cancellationToken = default);

    /// <summary>Lists users under <paramref name="ouPath"/>, or the configured directory base DN if null.</summary>
    Task<IReadOnlyList<UserDto>> ListAsync(string? ouPath, CancellationToken cancellationToken = default);

    /// <summary>Throws <see cref="CoreApi.Infrastructure.ConflictException"/> if the sAMAccountName is already in use.</summary>
    Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>Throws <see cref="CoreApi.Infrastructure.NotFoundException"/> if no matching user exists.</summary>
    Task<UserDto> UpdateAsync(string samAccountName, UpdateUserRequest request, CancellationToken cancellationToken = default);

    /// <summary>Throws <see cref="CoreApi.Infrastructure.NotFoundException"/> if no matching user exists.</summary>
    Task DeleteAsync(string samAccountName, CancellationToken cancellationToken = default);
}
