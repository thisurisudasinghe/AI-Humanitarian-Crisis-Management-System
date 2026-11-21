namespace Cms.Application.DTOs.Governance;

/// <summary>One row of the post-incident review: recommendation + human decision.</summary>
public class ReviewEntryDto
{
    public Guid RecommendationId { get; set; }

    public string Type { get; set; } = string.Empty;

    public Guid TargetId { get; set; }

    public string Source { get; set; } = string.Empty;

    public decimal Confidence { get; set; }

    public string Rationale { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = string.Empty;

    public Guid? DecisionId { get; set; }

    public string? Outcome { get; set; }

    public string? Reason { get; set; }

    public string? FinalPayloadJson { get; set; }

    public Guid? DecidedBy { get; set; }

    public DateTime? DecidedAt { get; set; }

    /// <summary>Final request status at the time of review, if the target is a request.</summary>
    public string FinalRequestStatus { get; set; } = string.Empty;

    /// <summary>True when the human modified or rejected the recommendation.</summary>
    public bool IsOverride { get; set; }
}
