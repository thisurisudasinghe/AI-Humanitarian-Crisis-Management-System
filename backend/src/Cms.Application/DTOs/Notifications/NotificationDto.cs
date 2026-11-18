namespace Cms.Application.DTOs.Notifications;

public class NotificationDto
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string Channel { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public DateTime? SentAt { get; set; }

    public DateTime? ReadAt { get; set; }

    public bool IsRead => ReadAt.HasValue;
}
