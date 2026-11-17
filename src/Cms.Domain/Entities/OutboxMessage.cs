
using Cms.Domain.Common;
using Cms.Domain.Enums;
namespace Cms.Domain.Entities;

public class OutboxMessage : BaseEntity
{

    public string Type { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;
    public Guid EntityId { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;

    public int Attempts { get; set; }

    public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Pending;

    public int RetryCount
    {
        get => Attempts;
        set => Attempts = value;
    }


    public DateTime? ProcessedAt { get; set; }

    public DateTime? NextRetryAt { get; set; }

    public DateTime? ProcessingStartedAt { get; set; }
    public DateTime? NextAttemptAt
    {
        get => NextRetryAt;
        set => NextRetryAt = value;
    }

    public string? ErrorMessage { get; set; }
    public string? LastError
    {
        get => ErrorMessage;
        set => ErrorMessage = value;
    }
}
