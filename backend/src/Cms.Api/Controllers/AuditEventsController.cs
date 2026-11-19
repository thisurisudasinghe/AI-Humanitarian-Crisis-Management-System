using Cms.Application.DTOs;
using Cms.Application.DTOs.Audit;
using Cms.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Controllers;

/// <summary>Append-only audit trail viewer.</summary>
[ApiController]
[Route("api/v1/audit-events")]
[Authorize(Policy = "AdminOnly")]
[Produces("application/json")]
public sealed class AuditEventsController : ControllerBase
{
    private readonly IAuditQueryService _auditQueryService;

    public AuditEventsController(IAuditQueryService auditQueryService)
    {
        _auditQueryService = auditQueryService;
    }

    /// <summary>Lists audit events with filters, newest first.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<AuditEventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResultDto<AuditEventDto>>> Get(
        [FromQuery] AuditEventQueryDto query,
        CancellationToken cancellationToken)
    {
        return Ok(await _auditQueryService.QueryAsync(query, cancellationToken));
    }
}
