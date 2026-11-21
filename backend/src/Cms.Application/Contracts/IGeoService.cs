namespace Cms.Application.Contracts;

/// <summary>Geocoding and distance. Implemented by Member 4 (M10 Mapping).</summary>
public interface IGeoService
{
    Task<GeoPoint?> GeocodeAsync(string address, CancellationToken cancellationToken = default);

    Task<DistanceResult> GetDistanceAsync(
        GeoPoint from,
        GeoPoint to,
        CancellationToken cancellationToken = default);
}
