using Cms.Application.Contracts;
using Cms.Application.Interfaces;
using Cms.Domain.Entities;
using Cms.Domain.Enums;
using Cms.Infrastructure.Data;

namespace Cms.Infrastructure.Notifications;

/// <summary>
/// SMTP-backed notification channel. Reliability comes from the transactional
/// outbox: a send failure marks the row Failed, rethrows, and the processor
/// retries before dead-lettering (NFR-REL-02/04).
/// </summary>
public sealed class EmailNotificationChannel : INotificationChannel
{
    private readonly INotificationSender _sender;
    private readonly CmsDbContext _db;
    private readonly IDateTimeService _dateTimeService;

    public EmailNotificationChannel(
        INotificationSender sender,
        CmsDbContext db,
        IDateTimeService dateTimeService)
    {
        _sender = sender;
        _db = db;
        _dateTimeService = dateTimeService;
    }

    public NotificationChannel Channel => NotificationChannel.Email;

    public async Task DeliverAsync(
        Notification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        try
        {
            await _sender.SendAsync(notification, cancellationToken);

            var now = _dateTimeService.UtcNow;
            notification.Status = NotificationStatus.Sent;
            notification.SentAt = now;
            notification.UpdatedAt = now;
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

            // Persist the failed delivery before rethrowing because the outbox
            // processor discards the handler scope without saving on failure.
            await _db.SaveChangesAsync(cancellationToken);

            throw new InvalidOperationException(
                $"Notification {notification.Id} could not be delivered over Email.",
                exception);
        }
    }
}
