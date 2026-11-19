using Cms.Domain.Enums;

namespace Cms.Application.DTOs.Notifications;

public class NotificationDto
{
    public Guid Id { get; set; }

    public Guid RecipientUserId { get; set; }

    public NotificationChannel Channel { get; set; }

    public string TemplateKey { get; set; } = string.Empty;

    public string? Subject { get; set; }

    public string? Body { get; set; }

    public NotificationStatus Status { get; set; }

    public DateTime? SentAt { get; set; }

    public DateTime? ReadAt { get; set; }

    public bool IsRead => ReadAt.HasValue;

    public Guid? RequestId { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>In-app inbox for the authenticated user.</summary>
public class NotificationInboxDto
{
    public IReadOnlyCollection<NotificationDto> Items { get; set; } =
        Array.Empty<NotificationDto>();

    public int UnreadCount { get; set; }

    public int TotalCount { get; set; }
}

public class NotificationQueryDto
{
    /// <summary>When true, the inbox omits notifications the user already read.</summary>
    public bool UnreadOnly { get; set; }

    /// <summary>Maximum number of inbox rows to return (1-200).</summary>
    public int Limit { get; set; } = 50;

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 50;
}
