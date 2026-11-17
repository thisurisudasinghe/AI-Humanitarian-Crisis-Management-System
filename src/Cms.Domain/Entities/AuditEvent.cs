namespace Cms.Domain.Entities;

/// <summary>
/// Append-only audit record. Never update or delete an audit event.
/// This entity deliberately does not inherit <c>BaseEntity</c> so it has no UpdatedAt.
/// </summary>
public class AuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public string? EntityId { get; set; }

    public Guid? ActorUserId { get; set; }

    public string? Details { get; set; }

    public string? IpAddress { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
