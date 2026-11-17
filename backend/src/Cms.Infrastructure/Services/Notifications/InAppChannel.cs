using Cms.Application.Interfaces.Notifications;
using Cms.Domain.Entities;
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
        var alreadyExists = await _context.Notifications
            .AnyAsync(
                n => n.IdempotencyKey == idempotencyKey,
                cancellationToken);

        if (alreadyExists)
            return;

        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Channel = "InApp",
            IdempotencyKey = idempotencyKey
        };

        await _context.Notifications.AddAsync(
            notification, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }
}