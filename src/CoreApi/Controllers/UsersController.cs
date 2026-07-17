using CoreApi.Models;
using CoreApi.Services;
using Microsoft.AspNetCore.Mvc;

namespace CoreApi.Controllers;

/// <summary>Create, read, update, and delete AD user accounts.</summary>
[ApiExplorerSettings(GroupName = "Users")]
public sealed class UsersController(IUserService users) : BaseApiController
{
    /// <summary>Lists users under a given OU, or the configured directory base DN.</summary>
    /// <param name="ouPath">Distinguished name of the container to search (optional; defaults to the configured base DN).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching users.</returns>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<UserDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> List(
        [FromQuery] string? ouPath, CancellationToken cancellationToken)
    {
        return Ok(await users.ListAsync(ouPath, cancellationToken));
    }

    /// <summary>Gets a single user by sAMAccountName.</summary>
    /// <param name="samAccountName">The user's sAMAccountName.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The matching user.</returns>
    [HttpGet("{samAccountName}")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<UserDto>> Get(string samAccountName, CancellationToken cancellationToken)
    {
        return Ok(await users.GetBySamAccountNameAsync(samAccountName, cancellationToken));
    }

    /// <summary>
    /// Creates a new AD user account. The account is always created disabled -- setting an
    /// initial password requires LDAPS, not yet configured.
    /// </summary>
    /// <param name="request">The user to create.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created user.</returns>
    [HttpPost]
    [ProducesResponseType<UserDto>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<UserDto>> Create(
        [FromBody] CreateUserRequest request, CancellationToken cancellationToken)
    {
        var created = await users.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { samAccountName = created.SamAccountName }, created);
    }

    /// <summary>Updates an existing AD user's attributes. Fields left null are not modified.</summary>
    /// <param name="samAccountName">The user's sAMAccountName.</param>
    /// <param name="request">The attributes to change.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The updated user.</returns>
    [HttpPut("{samAccountName}")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<UserDto>> Update(
        string samAccountName, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken)
    {
        return Ok(await users.UpdateAsync(samAccountName, request, cancellationToken));
    }

    /// <summary>Deletes an AD user account.</summary>
    /// <param name="samAccountName">The user's sAMAccountName.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content.</returns>
    [HttpDelete("{samAccountName}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Delete(string samAccountName, CancellationToken cancellationToken)
    {
        await users.DeleteAsync(samAccountName, cancellationToken);
        return NoContent();
    }
}
