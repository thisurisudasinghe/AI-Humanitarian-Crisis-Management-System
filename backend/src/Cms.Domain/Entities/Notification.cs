
using Cms.Domain.Common;
using Cms.Domain.Enums;

namespace Cms.Domain.Entities;

public class Notification : BaseEntity
{
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public NotificationStatus Status { get; set; } = NotificationStatus.Pending;
    public DateTime? SentAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public User User { get; set; } = null!;
}
