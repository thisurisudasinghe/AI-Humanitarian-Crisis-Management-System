using Cms.Application.Contracts;

namespace Cms.Infrastructure.Stubs;

/// <summary>Temporary resource matcher. Member 4 replaces this.</summary>
public sealed class EmptyResourceMatcher : IResourceMatcher
{
    public Task<IReadOnlyList<MatchCandidate>> RankAsync(
        Guid requestId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MatchCandidate>>([]);
}

/// <summary>Temporary shelter recommender. Member 4 replaces this.</summary>
public sealed class EmptyShelterRecommender : IShelterRecommender
{
    public Task<IReadOnlyList<MatchCandidate>> RankAsync(
        Guid requestId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MatchCandidate>>([]);
}

/// <summary>Temporary volunteer matcher. Member 4 replaces this.</summary>
public sealed class EmptyVolunteerMatcher : IVolunteerMatcher
{
    public Task<IReadOnlyList<MatchCandidate>> RankAsync(
        Guid requestId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<MatchCandidate>>([]);
}
