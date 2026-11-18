using Cms.Application.Interfaces.Notifications;
using Cms.Domain.Entities;
using Cms.Domain.Enums;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Cms.Infrastructure.Services.Notifications;

public class InAppChannel : INotificationChannel
{
    private readonly ApplicationDbContext _context;

    public InAppChannel(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task SendAsync(
        Guid userId,
        string title,
        string message,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (await _context.Notifications
                .AnyAsync(n => n.IdempotencyKey == idempotencyKey, cancellationToken))
        {
            return;
        }

        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Channel = "InApp",
            IdempotencyKey = idempotencyKey,
            Status = NotificationStatus.Sent,
            SentAt = DateTime.UtcNow
        };

        await _context.Notifications.AddAsync(notification, cancellationToken);

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsDuplicateKey(ex))
        {
            // A concurrent delivery won the race on the unique idempotency index.
            _context.Entry(notification).State = EntityState.Detached;
        }
    }

    private static bool IsDuplicateKey(DbUpdateException exception)
    {
        var text = exception.ToString();
        return text.Contains("2601", StringComparison.Ordinal)
            || text.Contains("2627", StringComparison.Ordinal)
            || text.Contains("IdempotencyKey", StringComparison.OrdinalIgnoreCase)
            || text.Contains("duplicate", StringComparison.OrdinalIgnoreCase);
    }
}
