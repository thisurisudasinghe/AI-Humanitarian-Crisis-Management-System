namespace Cms.Application.DTOs.AI;

/// <summary>
/// Frozen request body for POST /api/v1/recommendations/match.
/// Type is one of ResourceMatch, ShelterMatch or VolunteerMatch.
/// </summary>
public sealed class MatchRecommendationRequestDto
{
    public Guid RequestId { get; set; }

    public string Type { get; set; } = string.Empty;
}

public sealed class MatchRecommendationResultDto
{
    public Guid RecommendationId { get; set; }

    public string Type { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string RequestStatus { get; set; } = string.Empty;

    public IReadOnlyList<MatchCandidateDto> Candidates { get; set; } =
        Array.Empty<MatchCandidateDto>();
}
