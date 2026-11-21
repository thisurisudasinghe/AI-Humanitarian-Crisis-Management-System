using Cms.Domain.Constants;

namespace Cms.Application.DTOs.AI;

public class CreateRecommendationRequest
{
    public string Type { get; set; } = string.Empty;

    public Guid TargetId { get; set; }

    public string PayloadJson { get; set; } = string.Empty;

    public decimal Confidence { get; set; }

    public string Rationale { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public string Status { get; set; } = RecommendationStatuses.Pending;
}
