namespace Cms.Application.Contracts;

/// <summary>Creates AI/rule recommendations. Implemented by Member 3 (M3).</summary>
public interface IRecommendationService
{
    Task<Guid> CreateAsync(NewRecommendation recommendation, CancellationToken cancellationToken = default);
}
