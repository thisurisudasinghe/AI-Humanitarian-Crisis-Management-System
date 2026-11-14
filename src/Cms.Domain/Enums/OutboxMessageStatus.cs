
namespace Cms.Domain.Enums;

public enum OutboxMessageStatus
{
    Pending = 1,
    Processing = 2,
    Processed = 3,
    Failed = 4,
    DeadLettered = 5
}
