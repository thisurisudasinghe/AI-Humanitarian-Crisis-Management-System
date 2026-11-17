using System.Text.Json;
using Cms.Application.Interfaces;
using Cms.Domain.Entities;
using Cms.Infrastructure.Data;

namespace Cms.Infrastructure.Services;

public class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _context;
    private readonly IDateTimeService _dateTimeService;

    public NotificationService(
        ApplicationDbContext context,
        IDateTimeService dateTimeService)
    {
        _context = context;
        _dateTimeService = dateTimeService;
    }

    public async Task SendAsync(
        Guid userId,
        string title,
        string message,
        string channel,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var now = _dateTimeService.UtcNow;

        var notification = new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Channel = channel,
            IdempotencyKey = idempotencyKey,
            CreatedAt = now
        };

        var outboxMessage = new OutboxMessage
        {
            Type = "NotificationRequested",
            Payload = JsonSerializer.Serialize(new
            {
                NotificationId = notification.Id,
                UserId = userId,
                Title = title,
                Message = message,
                Channel = channel
            }),
            EntityId = notification.Id,
            IdempotencyKey = idempotencyKey,
            CreatedAt = now
        };

        await _context.Notifications.AddAsync(
            notification, cancellationToken);

        await _context.OutboxMessages.AddAsync(
            outboxMessage, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }
}