using Cms.Application.Cases.DTOs;
using Cms.Application.Cases.Interfaces;
using Cms.Application.DTOs.Public;
using Cms.Domain.Cases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Controllers;

[ApiController]
[Route("api/v1/service-centres")]
public sealed class ServiceCentresController : ControllerBase
{
    private readonly ICrisisService _crises;

    public ServiceCentresController(ICrisisService crises)
    {
        _crises = crises;
    }

    /// <summary>Lists all service centres for coordinator management, including hidden and inactive ones.</summary>
    [HttpGet]
    [Authorize(Roles = CasesRoles.CoordinatorOnly)]
    [ProducesResponseType(typeof(IReadOnlyList<StaffServiceCentreDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _crises.ListStaffServiceCentresAsync(cancellationToken));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CasesProblem.From(ex);
        }
    }

    /// <summary>Controls whether an active service centre appears on the public map.</summary>
    [HttpPatch("{id:guid}/public-visibility")]
    [Authorize(Roles = CasesRoles.CoordinatorOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetPublicVisibility(
        Guid id,
        [FromBody] SetServiceCentreVisibilityDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _crises.SetServiceCentrePublicVisibilityAsync(id, request.IsPublic, cancellationToken);
            return NoContent();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CasesProblem.From(ex);
        }
    }
}
