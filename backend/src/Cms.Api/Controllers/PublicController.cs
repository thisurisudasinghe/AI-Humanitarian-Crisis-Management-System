using Cms.Application.Cases.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/v1/public")]
public class PublicController : ControllerBase
{
    private readonly ICrisisService _crises;

    public PublicController(ICrisisService crises)
    {
        _crises = crises;
    }

    [HttpGet("alerts")]
    public async Task<IActionResult> Alerts(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _crises.ListPublicAlertsAsync(cancellationToken));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CasesProblem.From(ex);
        }
    }

    [HttpGet("service-centres")]
    public async Task<IActionResult> ServiceCentres(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _crises.ListPublicServiceCentresAsync(cancellationToken));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CasesProblem.From(ex);
        }
    }
}
