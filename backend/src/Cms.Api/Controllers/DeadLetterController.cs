
using Cms.Domain.Enums;
using Cms.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Cms.Api.Controllers;

[ApiController][Authorize(Roles = "SystemAdministrator")]

[Route("api/v1/admin/dead-letters")]
public class DeadLetterController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public DeadLetterController(ApplicationDbContext context)
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
            return NotFound();

        message.Status = OutboxMessageStatus.Pending;
        message.Attempts = 0;
        message.NextRetryAt = null;
        message.ErrorMessage = null;
        message.ProcessingStartedAt = null;

        await _context.SaveChangesAsync(cancellationToken);

        return Ok(new
        {
            message = "Message queued for retry."
        });
    }
}
