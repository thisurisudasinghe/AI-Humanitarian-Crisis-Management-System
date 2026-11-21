using Cms.Application.Cases.DTOs;
using Cms.Application.Cases.Interfaces;
using Cms.Domain.Cases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Controllers;

[ApiController]
[Route("api/v1/crises")]
public class CrisesController : ControllerBase
{
    private readonly ICrisisService _crises;

    public CrisesController(ICrisisService crises)
    {
        _crises = crises;
    }

    [HttpGet]
    [Authorize(Roles = CasesRoles.RequestReaders)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _crises.ListCrisesAsync(cancellationToken));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CasesProblem.From(ex);
        }
    }

    [HttpPost]
    [Authorize(Roles = CasesRoles.CoordinatorOnly)]
    public async Task<IActionResult> Create(
        [FromBody] CreateCrisisDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var id = await _crises.CreateCrisisAsync(request, cancellationToken);
            return CreatedAtAction(nameof(List), new { id }, new { id });
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
        [FromBody] UpdateCrisisDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            await _crises.UpdateCrisisAsync(id, request, cancellationToken);
            return NoContent();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CasesProblem.From(ex);
        }
    }

    [HttpPost("{id:guid}/areas")]
    [Authorize(Roles = CasesRoles.CoordinatorOnly)]
    public async Task<IActionResult> CreateArea(
        Guid id,
        [FromBody] CreateAreaDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var areaId = await _crises.CreateAreaAsync(id, request, cancellationToken);
            return Ok(new { id = areaId });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CasesProblem.From(ex);
        }
    }

    [HttpPost("{id:guid}/alerts")]
    [Authorize(Roles = CasesRoles.CoordinatorOnly)]
    public async Task<IActionResult> CreateAlert(
        Guid id,
        [FromBody] CreateAlertDto request,
        CancellationToken cancellationToken)
    {
        try
        {
            var alertId = await _crises.CreateAlertAsync(id, request, cancellationToken);
            return Ok(new { id = alertId });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CasesProblem.From(ex);
        }
    }

    [HttpGet("{id:guid}/summary")]
    [Authorize(Roles = CasesRoles.Staff)]
    public async Task<IActionResult> Summary(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _crises.GetSummaryAsync(id, cancellationToken));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CasesProblem.From(ex);
        }
    }
}
