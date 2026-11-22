using SharedMatchCandidate = Cms.Application.Contracts.MatchCandidate;
using SharedGeoPoint = Cms.Application.Contracts.GeoPoint;
using SharedDistanceResult = Cms.Application.Contracts.DistanceResult;
using SharedIGeoService = Cms.Application.Contracts.IGeoService;
using SharedIResourceMatcher = Cms.Application.Contracts.IResourceMatcher;
using SharedIShelterRecommender = Cms.Application.Contracts.IShelterRecommender;
using SharedIVolunteerMatcher = Cms.Application.Contracts.IVolunteerMatcher;
using OperationsIGeoService = Cms.Application.Contracts.Operations.IGeoService;
using OperationsIResourceMatcher = Cms.Application.Contracts.Operations.IResourceMatcher;
using OperationsIShelterRecommender = Cms.Application.Contracts.Operations.IShelterRecommender;
using OperationsIVolunteerMatcher = Cms.Application.Contracts.Operations.IVolunteerMatcher;

namespace Cms.Infrastructure.Operations;

/// <summary>
/// Bridges the Member 4 operations contracts to the frozen shared contracts so
/// other modules and all consumers resolve the real adapters, not foundation stubs.
/// </summary>
public sealed class FrozenGeoServiceAdapter(OperationsIGeoService inner) : SharedIGeoService
{
    public async Task<SharedGeoPoint?> GeocodeAsync(string address, CancellationToken cancellationToken = default)
    {
        var point = await inner.GeocodeAsync(address, cancellationToken);
        return point is null ? null : new SharedGeoPoint(point.Lat, point.Lng);
    }

    public async Task<SharedDistanceResult> GetDistanceAsync(SharedGeoPoint from, SharedGeoPoint to, CancellationToken cancellationToken = default)
    {
        var result = await inner.GetDistanceAsync(
            new Cms.Application.Contracts.Operations.GeoPoint(from.Lat, from.Lng),
            new Cms.Application.Contracts.Operations.GeoPoint(to.Lat, to.Lng),
            cancellationToken);
        return new SharedDistanceResult(result.Km, result.Minutes, result.IsApproximate);
    }
}

public sealed class FrozenResourceMatcherAdapter(OperationsIResourceMatcher inner) : SharedIResourceMatcher
{
    public async Task<IReadOnlyList<SharedMatchCandidate>> RankAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var rows = await inner.RankAsync(requestId, cancellationToken);
        return rows.Select(Convert).ToArray();
    }
    internal static SharedMatchCandidate Convert(Cms.Application.Contracts.Operations.MatchCandidate c) =>
        new(c.TargetId, (double)c.Score, c.Rationale, c.IsApproximate);
}

public sealed class FrozenShelterRecommenderAdapter(OperationsIShelterRecommender inner) : SharedIShelterRecommender
{
    public async Task<IReadOnlyList<SharedMatchCandidate>> RankAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var rows = await inner.RankAsync(requestId, cancellationToken);
        return rows.Select(FrozenResourceMatcherAdapter.Convert).ToArray();
    }
}

public sealed class FrozenVolunteerMatcherAdapter(OperationsIVolunteerMatcher inner) : SharedIVolunteerMatcher
{
    public async Task<IReadOnlyList<SharedMatchCandidate>> RankAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        var rows = await inner.RankAsync(requestId, cancellationToken);
        return rows.Select(FrozenResourceMatcherAdapter.Convert).ToArray();
    }
}
