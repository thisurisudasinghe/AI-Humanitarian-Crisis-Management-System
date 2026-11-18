namespace Cms.Application.DTOs.Notifications;

/// <summary>
/// Payload of the frozen <c>Notification.Send</c> outbox event.
/// TemplateKey + Data is the preferred (PII-safe) shape. Title/Message are
/// retained for backwards compatibility with events already in the outbox.
/// </summary>
public class NotificationPayloadDto
{
    public Guid RecipientUserId { get; set; }

    // Legacy alias used by early events.
    public Guid UserId
    {
        get => RecipientUserId;
        set => RecipientUserId = value;
    }

    public string TemplateKey { get; set; } = string.Empty;

    public string Language { get; set; } = "en";

    public Dictionary<string, string> Data { get; set; } = new();

    public string Channel { get; set; } = "InApp";

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public Guid? RequestId { get; set; }
}
