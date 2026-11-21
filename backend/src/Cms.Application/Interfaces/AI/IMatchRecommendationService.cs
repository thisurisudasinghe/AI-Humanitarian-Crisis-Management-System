using Cms.Application.DTOs.AI;

namespace Cms.Application.Interfaces.AI;

/// <summary>
/// Calls the Member 4 matchers, persists their ranked result as a
/// recommendation, and performs only the allowed human-governed status advance.
/// </summary>
public interface IMatchRecommendationService
{
    Task<MatchRecommendationResultDto> CreateAsync(
        MatchRecommendationRequestDto request,
        CancellationToken cancellationToken = default);
}
