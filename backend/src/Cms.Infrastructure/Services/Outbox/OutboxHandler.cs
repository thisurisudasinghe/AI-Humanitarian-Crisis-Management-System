using System.Text.Json;
using Cms.Application.Interfaces;
using Cms.Domain.Entities;

namespace Cms.Infrastructure.Services.Outbox;

public class OutboxHandler : IOutboxHandler
{
    private readonly INotificationService _notificationService;

    public OutboxHandler(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public async Task HandleAsync(
        OutboxMessage message,
        CancellationToken cancellationToken = default)
    {
        if (message.Type != "Notification.Send")
        {
            throw new InvalidOperationException(
                $"Unsupported outbox message type: {message.Type}");
        }

        var payload = JsonSerializer.Deserialize<NotificationPayload>(
            message.Payload);

        if (payload == null)
        {
            throw new InvalidOperationException(
                "Invalid notification payload.");
        }

        await _notificationService.SendAsync(
            payload.UserId,
            payload.Title,
            payload.Message,
            payload.Channel,
            message.IdempotencyKey,
            cancellationToken);
    }

    private class NotificationPayload
    {
        public Guid UserId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Channel { get; set; } = "InApp";
    }
}