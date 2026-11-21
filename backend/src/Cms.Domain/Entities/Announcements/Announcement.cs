using Cms.Domain.Common;
using Cms.Domain.Enums;

namespace Cms.Domain.Entities;

/// <summary>
/// An emergency broadcast. It cannot be delivered until a human approver has
/// explicitly approved it; approval fans out Notification.Send outbox messages.
/// </summary>
public class Announcement : BaseEntity
{
    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    /// <summary>Serialised AnnouncementAudienceDto — who should receive it.</summary>
    public string AudienceJson { get; set; } = "{}";

    public AnnouncementStatus Status { get; set; } = AnnouncementStatus.PendingApproval;

    public string Channel { get; set; } = "InApp";

    public Guid RequestedByUserId { get; set; }

    public Guid? ApprovedByUserId { get; set; }

    public DateTime? ApprovedAt { get; set; }

    public string? RejectionReason { get; set; }

    public DateTime? DispatchedAt { get; set; }

    /// <summary>Number of recipients the approval was fanned out to.</summary>
    public int RecipientCount { get; set; }

    /// <summary>Stable key so repeated create calls cannot produce duplicate broadcasts.</summary>
    public string IdempotencyKey { get; set; } = string.Empty;
}
