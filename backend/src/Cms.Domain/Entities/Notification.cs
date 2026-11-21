using Cms.Domain.Common;
using Cms.Domain.Enums;

namespace Cms.Domain.Entities;

/// <summary>
/// One delivery attempt for a recipient. A <c>Notification.Send</c> outbox event
/// produces an in-app row and, when the recipient has an email address, an email
/// row. <see cref="Status"/> tracks delivery only; the outbox processor owns
/// retries and dead-lettering.
/// </summary>
public class Notification : BaseEntity
{
    public Guid RecipientUserId { get; set; }

    /// <summary>
    /// Outbox event that produced this row. The (outbox message, channel) pair is
    /// unique so a retried event updates the existing row instead of duplicating
    /// it (FR-COM-07).
    /// </summary>
    public Guid? OutboxMessageId { get; set; }

    public NotificationChannel Channel { get; set; } = NotificationChannel.InApp;

    public string TemplateKey { get; set; } = string.Empty;

    public string? Subject { get; set; }

    public string? Body { get; set; }

    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;

    public DateTime? SentAt { get; set; }

    /// <summary>When the recipient opened an in-app notification.</summary>
    public DateTime? ReadAt { get; set; }

    /// <summary>Optional link to the request that triggered the notification.</summary>
    public Guid? RequestId { get; set; }

    public User User { get; set; } = null!;
}
