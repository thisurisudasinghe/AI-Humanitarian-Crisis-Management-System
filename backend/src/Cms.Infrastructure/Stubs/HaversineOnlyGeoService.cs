using Cms.Application.Contracts;

namespace Cms.Infrastructure.Stubs;

/// <summary>
/// Straight-line distance fallback used until Member 4 registers the real geo service.
/// Never claims to be exact: <see cref="DistanceResult.IsApproximate"/> is always true.
/// </summary>
public sealed class HaversineOnlyGeoService : IGeoService
{
    private const double EarthRadiusKm = 6371.0088;

    public Task<GeoPoint?> GeocodeAsync(string address, CancellationToken cancellationToken = default) =>
        Task.FromResult<GeoPoint?>(null);

    public Task<DistanceResult> GetDistanceAsync(
        GeoPoint from,
        GeoPoint to,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        var distance = HaversineKm(from, to);
        return Task.FromResult(new DistanceResult(distance, null, IsApproximate: true));
    }

    /// <summary>Great-circle distance in kilometres.</summary>
    public static double HaversineKm(GeoPoint from, GeoPoint to)
    {
        ArgumentNullException.ThrowIfNull(from);
        ArgumentNullException.ThrowIfNull(to);

        var lat1 = ToRadians(from.Lat);
        var lat2 = ToRadians(to.Lat);
        var deltaLat = ToRadians(to.Lat - from.Lat);
        var deltaLng = ToRadians(to.Lng - from.Lng);

        var a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                Math.Cos(lat1) * Math.Cos(lat2) *
                Math.Sin(deltaLng / 2) * Math.Sin(deltaLng / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return EarthRadiusKm * c;
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180d;
}
