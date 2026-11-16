
using Cms.Domain.Common;
using Cms.Domain.Enums;
namespace Cms.Domain.Entities;

public class OutboxMessage : BaseEntity
{

    public string Type { get; set; } = string.Empty;

    public string Payload { get; set; } = string.Empty;

    public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Pending;

    public int RetryCount { get; set; }


    public DateTime? ProcessedAt { get; set; }

    public DateTime? NextRetryAt { get; set; }

    public string? ErrorMessage { get; set; }
}
