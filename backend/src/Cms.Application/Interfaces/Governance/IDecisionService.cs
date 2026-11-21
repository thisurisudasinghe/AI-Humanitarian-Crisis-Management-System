using Cms.Application.DTOs.Governance;

namespace Cms.Application.Interfaces.Governance;

public interface IDecisionService
{
    /// <summary>Records an immutable human decision on an AI recommendation.</summary>
    Task<DecisionDto> RecordAsync(
        Guid recommendationId,
        DecisionRequestDto request,
        CancellationToken cancellationToken = default);

    Task<DecisionDto?> GetByIdAsync(
        Guid decisionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DecisionDto>> GetByRecommendationAsync(
        Guid recommendationId,
        CancellationToken cancellationToken = default);
}
