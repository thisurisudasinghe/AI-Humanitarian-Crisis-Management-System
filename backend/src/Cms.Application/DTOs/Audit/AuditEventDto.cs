namespace Cms.Application.DTOs.Audit;

public class AuditEventDto
{
    public Guid Id { get; set; }

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public string? EntityId { get; set; }

    public Guid? ActorUserId { get; set; }

    public string? Details { get; set; }

    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; }
}
