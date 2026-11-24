using Cms.Application.Cases.DTOs;
using Cms.Application.Cases.Interfaces;
using Cms.Application.DTOs.Public;
using Cms.Domain.Cases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Controllers;

[ApiController]
[Route("api/v1/alerts")]
public class AlertsController : ControllerBase
{
    private readonly ICrisisService _crises;

    public AlertsController(ICrisisService crises)
    {
        _crises = crises;
    }

    /// <summary>
    /// Lists every alert with its publication state for the staff publication
    /// form. Read-only and coordinator-only; never exposed anonymously.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = CasesRoles.CoordinatorOnly)]
    [ProducesResponseType(typeof(IReadOnlyList<AlertPublicationDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _crises.ListAlertsForPublicationAsync(cancellationToken));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CasesProblem.From(ex);
        }
    }

    [HttpPatch("{id:guid}")]
    [Authorize(Roles = CasesRoles.CoordinatorOnly)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateAlertDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _crises.UpdateAlertAsync(id, request, cancellationToken);
            return NoContent();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CasesProblem.From(ex);
        }
    }

    /// <summary>
    /// Publishes (or hides) the explicit public-map projection of an alert.
    /// Private Title/Body are never used for the public map.
    /// </summary>
    [HttpPost("{id:guid}/map-publication")]
    [Authorize(Roles = CasesRoles.CoordinatorOnly)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> PublishToMap(
        Guid id,
        [FromBody] PublishMapAlertDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _crises.PublishAlertToMapAsync(id, request, cancellationToken);
            return NoContent();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CasesProblem.From(ex);
        }
    }
}
