using System.Text.Json;
using System.Text.Json.Serialization;
using Cms.Application.Contracts;
using Cms.Application.DTOs.Notifications;
using Cms.Application.Interfaces;
using Cms.Domain.Entities;
using Cms.Domain.Enums;
using Cms.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cms.Infrastructure.Notifications;

/// <summary>
/// Dev 1 handler for the frozen <c>Notification.Send</c> outbox event (C3-04).
/// Creates an in-app notification for the recipient and, when an email address is
/// available, an email delivery row. Delivery failures mark the row
/// <see cref="NotificationStatus.Failed"/> and throw so the outbox processor
/// retries and eventually dead-letters the event (FR-COM-08).
/// </summary>
public sealed class EmailNotificationHandler : IOutboxHandler
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web)
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };

    private readonly CmsDbContext _db;
    private readonly INotificationSender _sender;
    private readonly IDateTimeService _dateTimeService;
    private readonly ILogger<EmailNotificationHandler> _logger;

    public EmailNotificationHandler(
        CmsDbContext db,
        INotificationSender sender,
        IDateTimeService dateTimeService,
        ILogger<EmailNotificationHandler> logger)
    {
        _db = db;
        _sender = sender;
        _dateTimeService = dateTimeService;
        _logger = logger;
    }

    public string Type => OutboxEventTypes.NotificationSend;

    public async Task HandleAsync(
        OutboxMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        var payload = Deserialize(message);

        if (payload.RecipientUserId == Guid.Empty)
        {
            throw new InvalidOperationException(
                $"Notification outbox message {message.Id} has no recipient.");
        }

        var user = await _db.Users
            .FirstOrDefaultAsync(
                candidate => candidate.Id == payload.RecipientUserId,
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"Notification recipient {payload.RecipientUserId} does not exist.");

        if (user.Status != UserStatus.Active)
        {
            _logger.LogInformation(
                "Notification {OutboxMessageId} skipped: recipient {RecipientUserId} is not active.",
                message.Id,
                user.Id);
            return;
        }

        var (subject, body) = NotificationTemplates.Render(payload);

        await UpsertAsync(
            message,
            user,
            payload,
            NotificationChannel.InApp,
            subject,
            body,
            cancellationToken);

        if (!string.IsNullOrWhiteSpace(user.EmailEncrypted))
        {
            await UpsertAsync(
                message,
                user,
                payload,
                NotificationChannel.Email,
                subject,
                body,
                cancellationToken);
        }
        else
        {
            _logger.LogInformation(
                "Notification {OutboxMessageId} has no email channel: recipient {RecipientUserId} has no address.",
                message.Id,
                user.Id);
        }
    }

    private async Task UpsertAsync(
        OutboxMessage message,
        User user,
        NotificationPayloadDto payload,
        NotificationChannel channel,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(
                candidate =>
                    candidate.OutboxMessageId == message.Id &&
                    candidate.Channel == channel,
                cancellationToken);

        if (notification?.Status == NotificationStatus.Sent)
        {
            // The event was already delivered. A processor retry is a no-op so a
            // duplicate email is never sent (FR-COM-07).
            return;
        }

        var now = _dateTimeService.UtcNow;

        if (notification is null)
        {
            notification = new Notification
            {
                RecipientUserId = user.Id,
                OutboxMessageId = message.Id,
                Channel = channel,
                TemplateKey = payload.TemplateKey,
                Subject = subject,
                Body = body,
                Status = NotificationStatus.Pending,
                RequestId = payload.RequestId,
                CreatedAt = now
            };

            _db.Notifications.Add(notification);
        }

        notification.User = user;
        notification.UpdatedAt = now;

        if (channel == NotificationChannel.InApp)
        {
            notification.Status = NotificationStatus.Sent;
            notification.SentAt = now;
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        try
        {
            await _sender.SendAsync(notification, cancellationToken);

            notification.Status = NotificationStatus.Sent;
            notification.SentAt = now;
            notification.UpdatedAt = _dateTimeService.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            notification.Status = NotificationStatus.Failed;
            notification.SentAt = null;
            notification.UpdatedAt = _dateTimeService.UtcNow;

            // Persist the failed delivery before rethrowing, because the outbox
            // processor discards the handler scope without saving on failure.
            await _db.SaveChangesAsync(cancellationToken);

            throw new InvalidOperationException(
                $"Notification {notification.Id} could not be delivered over {channel}.",
                exception);
        }
    }

    private static NotificationPayloadDto Deserialize(OutboxMessage message)
    {
        try
        {
            return JsonSerializer.Deserialize<NotificationPayloadDto>(
                       message.Payload,
                       SerializerOptions)
                   ?? throw new JsonException("The payload was empty.");
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"Notification outbox message {message.Id} has an invalid payload.",
                exception);
        }
    }
}
