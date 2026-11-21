using Cms.Application.Contracts;
using Cms.Application.Interfaces;
using Cms.Domain.Entities;
using Cms.Domain.Enums;
using Cms.Infrastructure.Data;

namespace Cms.Infrastructure.Notifications;

/// <summary>
/// In-app channel. Delivery is the persisted inbox row itself, so this channel
/// only needs to move that row to Sent.
/// </summary>
public sealed class InAppNotificationChannel : INotificationChannel
{
    private readonly CmsDbContext _db;
    private readonly IDateTimeService _dateTimeService;

    public InAppNotificationChannel(
        CmsDbContext db,
        IDateTimeService dateTimeService)
    {
        _db = db;
        _dateTimeService = dateTimeService;
    }

    public NotificationChannel Channel => NotificationChannel.InApp;

    public async Task DeliverAsync(
        Notification notification,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var now = _dateTimeService.UtcNow;
        notification.Status = NotificationStatus.Sent;
        notification.SentAt = now;
        notification.UpdatedAt = now;

        await _db.SaveChangesAsync(cancellationToken);
    }
}
