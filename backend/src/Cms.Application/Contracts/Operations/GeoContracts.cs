namespace Cms.Application.Contracts.Operations;
public sealed record GeoPoint(double Lat, double Lng);
public sealed record DistanceResult(double Km, double? Minutes, bool IsApproximate);
public interface IGeoService { Task<GeoPoint?> GeocodeAsync(string address, CancellationToken ct=default); Task<DistanceResult> GetDistanceAsync(GeoPoint from, GeoPoint to, CancellationToken ct=default); }
public sealed record MatchCandidate(Guid TargetId, decimal Score, string Rationale, bool IsApproximate);
public interface IResourceMatcher { Task<IReadOnlyList<MatchCandidate>> RankAsync(Guid requestId, CancellationToken ct=default); }
public interface IShelterRecommender { Task<IReadOnlyList<MatchCandidate>> RankAsync(Guid requestId, CancellationToken ct=default); }
public interface IVolunteerMatcher { Task<IReadOnlyList<MatchCandidate>> RankAsync(Guid requestId, CancellationToken ct=default); }

public interface IResourceMatchScoringStrategy
{
    (decimal Score, string Rationale) Score(Cms.Domain.Entities.Operations.Inventory inventory);
}
