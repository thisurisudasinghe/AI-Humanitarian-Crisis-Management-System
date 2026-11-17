using Cms.Application.DTOs.Admin;
using Cms.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Controllers;

/// <summary>Runtime configuration for service types, thresholds and SLA hours.</summary>
[ApiController]
[Route("api/v1/admin/config")]
[Authorize(Policy = "AdminOnly")]
[Produces("application/json")]
public sealed class AdminConfigController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminConfigController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    /// <summary>Returns all current system settings.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyCollection<SystemSettingDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyCollection<SystemSettingDto>>> Get(
        CancellationToken cancellationToken)
    {
        return Ok(await _adminService.GetConfigAsync(cancellationToken));
    }

    /// <summary>Upserts configuration values. Every write is audited.</summary>
    [HttpPatch]
    [Consumes("application/json")]
    [ProducesResponseType(typeof(IReadOnlyCollection<SystemSettingDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<IReadOnlyCollection<SystemSettingDto>>> Patch(
        [FromBody] UpdateConfigRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await _adminService.UpdateConfigAsync(request, cancellationToken));
    }
}
