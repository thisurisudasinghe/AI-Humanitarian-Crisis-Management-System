using Cms.Application.DTOs;
using Cms.Application.DTOs.Admin;
using Cms.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Controllers;

/// <summary>SystemAdmin user management.</summary>
[ApiController]
[Route("api/v1/users")]
[Authorize(Policy = "AdminOnly")]
[Consumes("application/json")]
[Produces("application/json")]
public sealed class UsersController : ControllerBase
{
    private readonly IAdminService _adminService;

    public UsersController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    /// <summary>Lists users with paging and filters.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<UserSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResultDto<UserSummaryDto>>> GetUsers(
        [FromQuery] string? search,
        [FromQuery] string? role,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _adminService.GetUsersAsync(
            search,
            role,
            isActive,
            page,
            pageSize,
            cancellationToken));
    }

    /// <summary>Gets one user.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(UserSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserSummaryDto>> GetUser(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await _adminService.GetUserAsync(id, cancellationToken));
    }

    /// <summary>Creates a staff user and assigns system roles.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(UserSummaryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UserSummaryDto>> CreateUser(
        [FromBody] CreateUserRequestDto request,
        CancellationToken cancellationToken)
    {
        var user = await _adminService.CreateUserAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
    }

    /// <summary>Updates a user's names, organisation or status.</summary>
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(UserSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserSummaryDto>> UpdateUser(
        Guid id,
        [FromBody] UpdateUserRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await _adminService.UpdateUserAsync(id, request, cancellationToken));
    }

    /// <summary>Replaces all roles assigned to a user.</summary>
    [HttpPut("{id:guid}/roles")]
    [ProducesResponseType(typeof(UserSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UserSummaryDto>> ReplaceRoles(
        Guid id,
        [FromBody] ReplaceRolesRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await _adminService.ReplaceUserRolesAsync(id, request, cancellationToken));
    }
}
