using System.Text.Json;
using Cms.Application.DTOs.Notifications;
using Cms.Application.Interfaces;
using Cms.Domain.Entities;

namespace Cms.Infrastructure.Services.Outbox;

public class OutboxHandler : IOutboxHandler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly INotificationService _notificationService;

    public OutboxHandler(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    public string MessageType => "Notification.Send";

    public async Task HandleAsync(
        OutboxMessage message,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(
                message.Type,
                MessageType,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Unsupported outbox message type: {message.Type}");
        }

        var payload = JsonSerializer.Deserialize<NotificationPayloadDto>(
            message.Payload,
            JsonOptions);

        if (payload == null || payload.RecipientUserId == Guid.Empty)
        {
            throw new InvalidOperationException("Invalid notification payload.");
        }

        if (!string.IsNullOrWhiteSpace(payload.TemplateKey))
        {
            await _notificationService.SendTemplateAsync(
                payload.RecipientUserId,
                payload.TemplateKey,
                payload.Channel,
                message.IdempotencyKey,
                payload.Data,
                payload.Language,
                cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(payload.Title) ||
            string.IsNullOrWhiteSpace(payload.Message))
        {
            throw new InvalidOperationException(
                "Notification payload must contain a template or title/message.");
        }

        await _notificationService.SendAsync(
            payload.RecipientUserId,
            payload.Title,
            payload.Message,
            payload.Channel,
            message.IdempotencyKey,
            cancellationToken);
    }
}
