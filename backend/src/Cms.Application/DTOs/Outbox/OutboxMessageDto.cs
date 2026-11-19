using Cms.Domain.Enums;

namespace Cms.Application.DTOs.Outbox;

/// <summary>
/// Read model for the dead-letter queue. The serialized payload and the
/// idempotency key are intentionally omitted from the admin API.
/// </summary>
public class OutboxMessageDto
{
    public Guid Id { get; set; }

    public string Type { get; set; } = string.Empty;

    public Guid EntityId { get; set; }

    public OutboxMessageStatus Status { get; set; }

    public int RetryCount { get; set; }

    public DateTime? NextRetryAt { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
