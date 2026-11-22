using System.Globalization;
using System.Text.Json;
using Cms.Application.Contracts.Operations;
using Microsoft.Extensions.Caching.Memory;

namespace Cms.Infrastructure.Operations;

/// <summary>Provider-backed geocoding/routing with bounded caches and a deterministic Haversine fallback.</summary>
public sealed class GeoService(IHttpClientFactory clients, IMemoryCache cache) : IGeoService
{
    private static readonly SemaphoreSlim NominatimGate = new(1, 1);
    private static DateTime _lastNominatimCallUtc = DateTime.MinValue;

    public async Task<GeoPoint?> GeocodeAsync(string address, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(address) || address.Length > 500) return null;
        var normalized = string.Join(' ', address.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var key = "geo:geocode:" + normalized.ToLowerInvariant();
        if (cache.TryGetValue(key, out GeoPoint? cached)) return cached;
        try
        {
            await NominatimGate.WaitAsync(ct);
            try
            {
                var wait = TimeSpan.FromSeconds(1) - (DateTime.UtcNow - _lastNominatimCallUtc);
                if (wait > TimeSpan.Zero) await Task.Delay(wait, ct);
                _lastNominatimCallUtc = DateTime.UtcNow;
            }
            finally { NominatimGate.Release(); }
            var client = clients.CreateClient("nominatim");
            var uri = "search?format=jsonv2&limit=1&q=" + Uri.EscapeDataString(normalized);
            using var response = await client.GetAsync(uri, ct);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var first = doc.RootElement.EnumerateArray().FirstOrDefault();
            if (first.ValueKind == JsonValueKind.Undefined ||
                !double.TryParse(first.GetProperty("lat").GetString(), CultureInfo.InvariantCulture, out var lat) ||
                !double.TryParse(first.GetProperty("lon").GetString(), CultureInfo.InvariantCulture, out var lng) ||
                !Valid(lat, lng))
            { cache.Set(key, (GeoPoint?)null, TimeSpan.FromMinutes(1)); return null; }
            var point = new GeoPoint(lat, lng); cache.Set(key, point, TimeSpan.FromHours(24)); return point;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch { cache.Set(key, (GeoPoint?)null, TimeSpan.FromMinutes(1)); return null; }
    }

    public async Task<DistanceResult> GetDistanceAsync(GeoPoint from, GeoPoint to, CancellationToken ct = default)
    {
        if (!Valid(from.Lat, from.Lng) || !Valid(to.Lat, to.Lng)) throw new ArgumentOutOfRangeException(nameof(from));
        var key = $"geo:route:{from.Lat:F5}:{from.Lng:F5}:{to.Lat:F5}:{to.Lng:F5}";
        if (cache.TryGetValue(key, out DistanceResult? saved) && saved is not null) return saved;
        try
        {
            var client = clients.CreateClient("osrm");
            var path = $"route/v1/driving/{from.Lng.ToString(CultureInfo.InvariantCulture)},{from.Lat.ToString(CultureInfo.InvariantCulture)};{to.Lng.ToString(CultureInfo.InvariantCulture)},{to.Lat.ToString(CultureInfo.InvariantCulture)}?overview=false";
            using var response = await client.GetAsync(path, ct); response.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var route = doc.RootElement.GetProperty("routes").EnumerateArray().First();
            var result = new DistanceResult(Math.Round(route.GetProperty("distance").GetDouble() / 1000d, 2), Math.Round(route.GetProperty("duration").GetDouble() / 60d, 1), false);
            cache.Set(key, result, TimeSpan.FromHours(24)); return result;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch
        {
            var fallback = new DistanceResult(Math.Round(HaversineKm(from, to), 2), null, true);
            cache.Set(key, fallback, TimeSpan.FromMinutes(1)); return fallback;
        }
    }

    public static double HaversineKm(GeoPoint a, GeoPoint b)
    {
        const double radius = 6371.0088;
        static double Rad(double degrees) => degrees * Math.PI / 180d;
        var dLat = Rad(b.Lat - a.Lat); var dLng = Rad(b.Lng - a.Lng);
        var h = Math.Pow(Math.Sin(dLat / 2), 2) + Math.Cos(Rad(a.Lat)) * Math.Cos(Rad(b.Lat)) * Math.Pow(Math.Sin(dLng / 2), 2);
        return radius * 2 * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }
    private static bool Valid(double lat, double lng) => double.IsFinite(lat) && double.IsFinite(lng) && lat is >= -90 and <= 90 && lng is >= -180 and <= 180;
}
