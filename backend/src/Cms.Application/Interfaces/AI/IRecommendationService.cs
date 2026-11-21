using Cms.Application.DTOs.AI;
using Cms.Domain.Entities;

namespace Cms.Application.Interfaces.AI;

public interface IRecommendationService
{
    Task<Guid> CreateAsync(
        CreateRecommendationRequest request,
        CancellationToken cancellationToken = default);

    Task<Recommendation?> GetByIdAsync(
        Guid recommendationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Recommendation>> GetByTargetAsync(
        Guid targetId,
        string? type = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Recommendation>> GetAllAsync(
        string? type = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MatchCandidateDto>> GetMatchesAsync(
        MatchRequestDto request,
        CancellationToken cancellationToken = default);

    Task<Guid> GenerateForCrisisAsync(
        Guid crisisId,
        CancellationToken cancellationToken = default);
}
