using Cms.Domain.Entities;

namespace Cms.Application.Contracts;

/// <summary>
/// Delivers one notification over a single channel. Implemented by
/// <c>SmtpNotificationSender</c> (real SMTP) and <c>LogNotificationSender</c>
/// (development-only log stub when SMTP is not configured).
/// </summary>
public interface INotificationSender
{
    /// <summary>Channel this sender handles, matching <c>Notification.Channel</c>.</summary>
    string Channel { get; }

    /// <summary>
    /// Delivers the notification or throws. A throw lets the outbox processor
    /// retry the event and eventually dead-letter it (FR-COM-08).
    /// </summary>
    Task SendAsync(Notification notification, CancellationToken cancellationToken = default);
}
