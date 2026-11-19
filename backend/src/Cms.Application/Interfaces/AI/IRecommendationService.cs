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

    Task<Guid> GenerateForCrisisAsync(
        Guid crisisId,
        CancellationToken cancellationToken = default);
}