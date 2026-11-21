using Cms.Application.Cases.Interfaces;
using Cms.Domain.Cases;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Controllers;

[ApiController]
[Route("api/v1/areas")]
public class AreasController : ControllerBase
{
    private readonly ICrisisService _crises;

    public AreasController(ICrisisService crises)
    {
        _crises = crises;
    }

    [HttpGet]
    [Authorize(Roles = CasesRoles.RequestReaders)]
    public async Task<IActionResult> List([FromQuery] Guid? crisisId, CancellationToken cancellationToken)
    {
        try
        {
            return Ok(await _crises.ListAreasAsync(crisisId, cancellationToken));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return CasesProblem.From(ex);
        }
    }
}
