using Cms.Domain.Entities;
using Cms.Domain.Enums;

namespace Cms.Application.Contracts;

/// <summary>
/// One delivery channel for the Notification.Send outbox event. Channels are
/// resolved through <see cref="INotificationChannelFactory"/> so callers never
/// branch on concrete SMTP or in-app implementations.
/// </summary>
public interface INotificationChannel
{
    NotificationChannel Channel { get; }

    /// <summary>
    /// Delivers the already persisted notification row. Implementations mark the
    /// row Sent or Failed and save it themselves.
    /// </summary>
    Task DeliverAsync(
        Notification notification,
        CancellationToken cancellationToken = default);
}
