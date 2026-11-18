using Cms.Application.DTOs.Kpi;
using Cms.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Controllers;

/// <summary>Reporting and KPI endpoint (M11).</summary>
[ApiController]
[Route("api/v1/kpis")]
[Route("api/v1/reports/kpis")]
[Authorize(Policy = "KpiViewer")]
[Produces("application/json")]
public sealed class KpiController : ControllerBase
{
    private readonly IKpiService _kpiService;

    public KpiController(IKpiService kpiService)
    {
        _kpiService = kpiService;
    }

    /// <summary>Returns KPI aggregates for the supplied UTC period.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(KpiReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<KpiReportDto>> Get(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to,
        [FromQuery] Guid? crisisEventId = null,
        [FromQuery] Guid? areaId = null,
        CancellationToken cancellationToken = default)
    {
        return Ok(await _kpiService.GetReportAsync(
            from,
            to,
            crisisEventId,
            areaId,
            cancellationToken));
    }
}
