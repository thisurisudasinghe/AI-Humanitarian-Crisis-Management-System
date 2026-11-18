using Cms.Domain.Enums;

namespace Cms.Application.DTOs.Governance;

public class DecisionRequestDto
{
    /// <summary>Approved, Modified, Rejected or Deferred.</summary>
    public string Outcome { get; set; } = string.Empty;

    public string? FinalPayloadJson { get; set; }

    public string Reason { get; set; } = string.Empty;
}

public class DecisionDto
{
    public Guid Id { get; set; }

    public Guid RecommendationId { get; set; }

    public DecisionOutcome Outcome { get; set; }

    public string? FinalPayloadJson { get; set; }

    public string Reason { get; set; } = string.Empty;

    public Guid DecidedBy { get; set; }

    public DateTime DecidedAt { get; set; }
}
