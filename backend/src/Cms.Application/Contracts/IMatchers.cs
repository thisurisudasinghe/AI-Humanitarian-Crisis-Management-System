namespace Cms.Application.Contracts;

/// <summary>Resource matching strategy. Implemented by Member 4 (M5).</summary>
public interface IResourceMatcher
{
    Task<IReadOnlyList<MatchCandidate>> RankAsync(Guid requestId, CancellationToken cancellationToken = default);
}

/// <summary>Shelter recommendation strategy. Implemented by Member 4 (M6).</summary>
public interface IShelterRecommender
{
    Task<IReadOnlyList<MatchCandidate>> RankAsync(Guid requestId, CancellationToken cancellationToken = default);
}

/// <summary>Volunteer matching strategy. Implemented by Member 4 (M7).</summary>
public interface IVolunteerMatcher
{
    Task<IReadOnlyList<MatchCandidate>> RankAsync(Guid requestId, CancellationToken cancellationToken = default);
}
