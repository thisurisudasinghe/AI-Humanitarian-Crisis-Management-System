using Cms.Application.Cases.Interfaces;
using Cms.Application.DTOs.Public;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Cms.Api.Controllers;

/// <summary>
/// Anonymous, public-safe live-map feed. The service returns projections only:
/// no requests, volunteers, internal identifiers or private alert fields.
/// </summary>
[ApiController]
[AllowAnonymous]
[EnableRateLimiting("public")]
[Route("api/v1/public")]
[Produces("application/json")]
public sealed class PublicMapController : ControllerBase
{
    private readonly ICrisisService _crises;

    public PublicMapController(ICrisisService crises)
    {
        _crises = crises;
    }

    [HttpGet("map")]
    [ProducesResponseType(typeof(PublicMapDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Map(CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _crises.GetPublicMapAsync(cancellationToken));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CasesProblem.From(ex);
        }
    }
}
