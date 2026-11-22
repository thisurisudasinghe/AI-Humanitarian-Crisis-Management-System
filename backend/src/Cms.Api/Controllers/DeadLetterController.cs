using Cms.Application.Common;
using Cms.Application.Contracts;
using Cms.Application.DTOs;
using Cms.Application.DTOs.Outbox;
using Cms.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Cms.Api.Controllers;

/// <summary>
/// Dead-letter view and retry for the shared transactional outbox (M9/M4).
/// Kept alongside <see cref="OutboxController"/> so the existing admin route
/// remains available while both modules share the transactional outbox.
/// </summary>
/// <remarks>
/// Dead-lettered notifications are visible to administrators and are retried
/// explicitly; payload and idempotency key are omitted from the admin projection.
/// </remarks>
[ApiController]
[Authorize(Policy = "AdminOnly")]
[Route("api/v1/admin/dead-letters")]
[Produces("application/json")]
public sealed class DeadLetterController : ControllerBase
{
    private readonly IOutboxAdminService _outboxAdminService;
    private readonly IAuditLogger _auditLogger;

    public DeadLetterController(
        IOutboxAdminService outboxAdminService,
        IAuditLogger auditLogger)
    {
        _outboxAdminService = outboxAdminService;
        _auditLogger = auditLogger;
    }

    /// <summary>Lists dead-lettered outbox messages in paged form.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResultDto<OutboxMessageDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResultDto<OutboxMessageDto>>> GetDeadLetters(
        [FromQuery] OutboxQueryDto query,
        CancellationToken cancellationToken)
    {
        // The admin projection deliberately omits the serialized payload and
        // the idempotency key. Never return raw OutboxMessage rows here.
        return Ok(await _outboxAdminService.GetDeadLetteredAsync(query, cancellationToken));
    }

    /// <summary>Requeues one dead-lettered message for another delivery attempt.</summary>
    [HttpPost("{id:guid}/retry")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RetryDeadLetter(
        Guid id,
        CancellationToken cancellationToken)
    {
        var requeued = await _outboxAdminService.RetryDeadLetteredAsync(id, cancellationToken);
        if (!requeued)
        {
            return NotFound();
        }

        await _auditLogger.LogAsync(
            AuditActions.OutboxRetried,
            "common.OutboxMessage",
            id.ToString(),
            new { status = "Pending" },
            cancellationToken);

        return Ok(new { message = "Message queued for retry." });
    }
}
