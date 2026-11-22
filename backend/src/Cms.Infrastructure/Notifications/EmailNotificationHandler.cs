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
    private readonly INotificationChannelFactory _channelFactory;
    private readonly IDateTimeService _dateTimeService;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<EmailNotificationHandler> _logger;

    public EmailNotificationHandler(
        CmsDbContext db,
        INotificationChannelFactory channelFactory,
        IDateTimeService dateTimeService,
        IAuditLogger auditLogger,
        ILogger<EmailNotificationHandler> logger)
    {
        _db = db;
        _channelFactory = channelFactory;
        _dateTimeService = dateTimeService;
        _auditLogger = auditLogger;
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
        var channels = _channelFactory.GetChannels(payload.Channel);

        foreach (var channel in channels)
        {
            if (channel.Channel == NotificationChannel.Email &&
                string.IsNullOrWhiteSpace(user.EmailEncrypted))
            {
                _logger.LogInformation(
                    "Notification {OutboxMessageId} has no email channel: recipient {RecipientUserId} has no address.",
                    message.Id,
                    user.Id);
                continue;
            }

            var notification = await UpsertAsync(
                message,
                user,
                payload,
                channel.Channel,
                subject,
                body,
                cancellationToken);

            if (notification.Status == NotificationStatus.Sent)
            {
                // A processor retry must never send the same event twice.
                continue;
            }

            await channel.DeliverAsync(notification, cancellationToken);

            await _auditLogger.LogAsync(
                null,
                "Notification.Delivered",
                "Notification",
                notification.Id.ToString(),
                new
                {
                    OutboxMessageId = message.Id,
                    payload.TemplateKey,
                    Channel = channel.Channel.ToString(),
                    payload.RecipientUserId
                },
                cancellationToken);
        }

        await MarkAnnouncementDispatchedAsync(message, cancellationToken);
    }

    /// <summary>
    /// Announcement fan-out creates one outbox message per recipient. The
    /// broadcast moves to Dispatched only when every fan-out message is
    /// Processed. Failed, pending and dead-lettered messages keep it Approved so
    /// operators never see failed delivery reported as completed.
    /// </summary>
    private async Task MarkAnnouncementDispatchedAsync(
        OutboxMessage message,
        CancellationToken cancellationToken)
    {
        var announcement = await _db.Announcements
            .FirstOrDefaultAsync(
                candidate =>
                    candidate.Id == message.EntityId &&
                    candidate.Status == AnnouncementStatus.Approved,
                cancellationToken);

        if (announcement == null)
        {
            return;
        }

        var undelivered = await _db.OutboxMessages
            .AsNoTracking()
            .AnyAsync(
                candidate =>
                    candidate.EntityId == message.EntityId &&
                    candidate.Id != message.Id &&
                    candidate.Status != OutboxMessageStatus.Processed,
                cancellationToken);

        if (undelivered)
        {
            return;
        }

        announcement.Status = AnnouncementStatus.Dispatched;
        announcement.DispatchedAt = _dateTimeService.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Notification> UpsertAsync(
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
        notification.Subject = subject;
        notification.Body = body;

        await _db.SaveChangesAsync(cancellationToken);
        return notification;
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
