using Cms.Application.DTOs;
using Cms.Application.DTOs.Admin;
using Cms.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Controllers;

/// <summary>SystemAdmin organisation management.</summary>
[ApiController]
[Route("api/v1/organisations")]
[Authorize(Policy = "AdminOnly")]
[Consumes("application/json")]
[Produces("application/json")]
public sealed class OrganisationsController : ControllerBase
{
    private readonly IAdminService _adminService;

    public OrganisationsController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    /// <summary>Lists organisations with paging and filters.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<OrganisationDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResultDto<OrganisationDto>>> GetOrganisations(
        [FromQuery] string? search,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _adminService.GetOrganisationsAsync(
            search,
            isActive,
            page,
            pageSize,
            cancellationToken));
    }

    /// <summary>Gets one organisation.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(OrganisationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganisationDto>> GetOrganisation(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await _adminService.GetOrganisationAsync(id, cancellationToken));
    }

    /// <summary>Creates an organisation.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(OrganisationDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrganisationDto>> CreateOrganisation(
        [FromBody] CreateOrganisationRequestDto request,
        CancellationToken cancellationToken)
    {
        var organisation = await _adminService.CreateOrganisationAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetOrganisation), new { id = organisation.Id }, organisation);
    }

    /// <summary>Updates an organisation or deactivates it.</summary>
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(typeof(OrganisationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<OrganisationDto>> UpdateOrganisation(
        Guid id,
        [FromBody] UpdateOrganisationRequestDto request,
        CancellationToken cancellationToken)
    {
        return Ok(await _adminService.UpdateOrganisationAsync(id, request, cancellationToken));
    }
}
