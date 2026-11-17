using Cms.Domain.Common;
using Cms.Domain.Enums;

namespace Cms.Domain.Entities;

public class DecisionRecord : BaseEntity
{
    public Guid RecommendationId { get; set; }

    public DecisionOutcome Outcome { get; set; }

    public string? FinalPayloadJson { get; set; }

    public string Reason { get; set; } = string.Empty;

    public Guid DecidedByUserId { get; set; }

    public DateTime DecidedAt { get; set; }
}