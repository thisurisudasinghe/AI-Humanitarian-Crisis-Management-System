using Cms.Application.DTOs;
using Cms.Application.DTOs.Outbox;
using Cms.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Controllers;

/// <summary>SystemAdmin view of the transactional outbox dead-letter queue.</summary>
[ApiController]
[Route("api/v1/admin/outbox")]
[Authorize(Policy = "AdminOnly")]
[Produces("application/json")]
public sealed class OutboxController : ControllerBase
{
    private readonly IOutboxAdminService _outboxAdminService;

    public OutboxController(IOutboxAdminService outboxAdminService)
    {
        _outboxAdminService = outboxAdminService;
    }

    /// <summary>Lists dead-lettered outbox messages, newest first.</summary>
    [HttpGet("dead")]
    [ProducesResponseType(typeof(PagedResultDto<OutboxMessageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResultDto<OutboxMessageDto>>> GetDeadLettered(
        [FromQuery] OutboxQueryDto query,
        CancellationToken cancellationToken)
    {
        return Ok(await _outboxAdminService.GetDeadLetteredAsync(query, cancellationToken));
    }
}
