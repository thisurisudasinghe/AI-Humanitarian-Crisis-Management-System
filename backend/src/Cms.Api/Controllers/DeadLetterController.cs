using Cms.Domain.Enums;
using Cms.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cms.Api.Controllers;

/// <summary>
/// Dead-letter view and retry for the shared transactional outbox (M9/M4).
/// Kept alongside <see cref="OutboxController"/> so the Member 3 route remains
/// available while both modules share the same <see cref="CmsDbContext"/>.
/// </summary>
[ApiController]
[Authorize(Roles = "SystemAdministrator")]
[Route("api/v1/admin/dead-letters")]
[Produces("application/json")]
public class DeadLetterController : ControllerBase
{
    private readonly CmsDbContext _context;

    public DeadLetterController(CmsDbContext context)
    {
        _context = context;
    }

    // GET: api/v1/admin/dead-letters
    [HttpGet]
    public async Task<IActionResult> GetDeadLetters(
        CancellationToken cancellationToken)
    {
        var messages = await _context.OutboxMessages
            .AsNoTracking()
            .Where(x => x.Status == OutboxMessageStatus.DeadLettered)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        return Ok(messages);
    }

    // POST: api/v1/admin/dead-letters/{id}/retry
    [HttpPost("{id:guid}/retry")]
    public async Task<IActionResult> RetryDeadLetter(
        Guid id,
        CancellationToken cancellationToken)
    {
        var message = await _context.OutboxMessages
            .FirstOrDefaultAsync(
                x => x.Id == id &&
                     x.Status == OutboxMessageStatus.DeadLettered,
                cancellationToken);

        if (message == null)
        {
            return NotFound();
        }

        message.Status = OutboxMessageStatus.Pending;
        message.RetryCount = 0;
        message.NextRetryAt = null;
        message.ErrorMessage = null;

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Message queued for retry."
        });
    }
}
