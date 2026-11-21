using Cms.Domain.Common;
using Cms.Domain.Enums;

namespace Cms.Domain.Entities;

/// <summary>
/// Transactional outbox row. A business change and its outbox row are committed
/// in the same <c>SaveChanges</c> call; the background processor dispatches the
/// row to the registered <c>IOutboxHandler</c>.
/// </summary>
public class OutboxMessage : BaseEntity
{
    public string Type { get; set; } = string.Empty;

    public Guid EntityId { get; set; }

    public string Payload { get; set; } = string.Empty;

    public string IdempotencyKey { get; set; } = string.Empty;

    public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Pending;

    public int RetryCount { get; set; }

    public DateTime? NextRetryAt { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public string? ErrorMessage { get; set; }
}
