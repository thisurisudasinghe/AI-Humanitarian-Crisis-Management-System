using Cms.Application.Cases.DTOs;
using Cms.Application.Cases.Interfaces;
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
}
