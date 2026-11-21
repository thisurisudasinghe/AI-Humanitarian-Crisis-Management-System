namespace Cms.Application.DTOs.Notifications;

/// <summary>Notification payload carried by a <c>Notification.Send</c> outbox event.</summary>
public class NotificationPayloadDto
{
    public Guid RecipientUserId { get; set; }

    public string TemplateKey { get; set; } = string.Empty;

    /// <summary>Reserved for the P2 multilingual template work.</summary>
    public string? Language { get; set; }

    /// <summary>
    /// Optional delivery channel (InApp or Email). Null means every supported
    /// channel, preserving the behaviour of existing notification producers.
    /// </summary>
    public string? Channel { get; set; }

    /// <summary>Optional override; otherwise the template registry supplies a subject.</summary>
    public string? Subject { get; set; }

    /// <summary>Optional override; otherwise the template registry supplies a body.</summary>
    public string? Body { get; set; }

    public Guid? RequestId { get; set; }

    public Dictionary<string, string> Data { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}
