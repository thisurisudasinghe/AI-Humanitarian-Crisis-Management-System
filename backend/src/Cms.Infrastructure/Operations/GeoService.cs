using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using Cms.Application.Contracts.Operations;
using Microsoft.Extensions.Caching.Memory;

namespace Cms.Infrastructure.Operations;

/// <summary>Provider-backed geocoding/routing with bounded caches and a deterministic Haversine fallback.</summary>
public sealed class GeoService : IGeoService
{
    private static readonly SemaphoreSlim NominatimGate = new(1, 1);
    private static DateTimeOffset _lastNominatimCallUtc = DateTimeOffset.MinValue;

    private readonly IHttpClientFactory _clients;
    private readonly IMemoryCache _cache;
    private readonly TimeProvider _clock;

    public GeoService(IHttpClientFactory clients, IMemoryCache cache)
        : this(clients, cache, TimeProvider.System)
    {
    }

    public GeoService(IHttpClientFactory clients, IMemoryCache cache, TimeProvider clock)
    {
        _clients = clients;
        _cache = cache;
        _clock = clock;
    }

    public async Task<GeoPoint?> GeocodeAsync(string address, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(address) || address.Length > 500) return null;
        var normalized = string.Join(' ', address.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        var key = "geo:geocode:" + normalized.ToLowerInvariant();
        if (_cache.TryGetValue(key, out GeoPoint? cached)) return cached;

        try
        {
            var client = _clients.CreateClient("nominatim");
            using var request = new HttpRequestMessage(HttpMethod.Get,
                "search?format=jsonv2&limit=1&q=" + Uri.EscapeDataString(normalized));
            var culture = CultureInfo.CurrentCulture.Name;
            if (!string.IsNullOrWhiteSpace(culture)) request.Headers.AcceptLanguage.Add(new StringWithQualityHeaderValue(culture));
            using var response = await SendNominatimAsync(client, request, ct);
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var first = doc.RootElement.EnumerateArray().FirstOrDefault();
            if (first.ValueKind == JsonValueKind.Undefined ||
                !double.TryParse(first.GetProperty("lat").GetString(), CultureInfo.InvariantCulture, out var lat) ||
                !double.TryParse(first.GetProperty("lon").GetString(), CultureInfo.InvariantCulture, out var lng) ||
                !Valid(lat, lng))
            {
                _cache.Set(key, (GeoPoint?)null, TimeSpan.FromMinutes(1));
                return null;
            }
            var point = new GeoPoint(lat, lng);
            _cache.Set(key, point, TimeSpan.FromHours(24));
            return point;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch
        {
            _cache.Set(key, (GeoPoint?)null, TimeSpan.FromMinutes(1));
            return null;
        }
    }

    public async Task<DistanceResult> GetDistanceAsync(GeoPoint from, GeoPoint to, CancellationToken ct = default)
    {
        if (!Valid(from.Lat, from.Lng) || !Valid(to.Lat, to.Lng)) throw new ArgumentOutOfRangeException(nameof(from));
        var key = $"geo:route:{from.Lat:F5}:{from.Lng:F5}:{to.Lat:F5}:{to.Lng:F5}";
        if (_cache.TryGetValue(key, out DistanceResult? saved) && saved is not null) return saved;
        try
        {
            var client = _clients.CreateClient("osrm");
            var path = $"route/v1/driving/{from.Lng.ToString(CultureInfo.InvariantCulture)},{from.Lat.ToString(CultureInfo.InvariantCulture)};{to.Lng.ToString(CultureInfo.InvariantCulture)},{to.Lat.ToString(CultureInfo.InvariantCulture)}?overview=false";
            using var response = await client.GetAsync(path, ct);
            response.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
            var route = doc.RootElement.GetProperty("routes").EnumerateArray().First();
            var result = new DistanceResult(
                Math.Round(route.GetProperty("distance").GetDouble() / 1000d, 2),
                Math.Round(route.GetProperty("duration").GetDouble() / 60d, 1),
                false);
            _cache.Set(key, result, TimeSpan.FromHours(24));
            return result;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch
        {
            var fallback = new DistanceResult(Math.Round(HaversineKm(from, to), 2), null, true);
            _cache.Set(key, fallback, TimeSpan.FromMinutes(1));
            return fallback;
        }
    }

    public static double HaversineKm(GeoPoint a, GeoPoint b) => HaversineKm(a.Lat, a.Lng, b.Lat, b.Lng);

    public static double HaversineKm(double lat1, double lng1, double lat2, double lng2)
    {
        if (!double.IsFinite(lat1) || !double.IsFinite(lng1) || !double.IsFinite(lat2) || !double.IsFinite(lng2) ||
            lat1 is < -90 or > 90 || lat2 is < -90 or > 90 || lng1 is < -180 or > 180 || lng2 is < -180 or > 180)
            throw new ArgumentOutOfRangeException(nameof(lat1), "Coordinates must be finite and within latitude/longitude bounds.");
        const double radius = 6371.0088;
        static double Rad(double value) => value * Math.PI / 180d;
        var dLat = Rad(lat2 - lat1);
        var dLng = Rad(lng2 - lng1);
        var h = Math.Pow(Math.Sin(dLat / 2), 2) + Math.Cos(Rad(lat1)) * Math.Cos(Rad(lat2)) * Math.Pow(Math.Sin(dLng / 2), 2);
        return radius * 2 * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    private async Task<HttpResponseMessage> SendNominatimAsync(
        HttpClient client, HttpRequestMessage request, CancellationToken ct)
    {
        await NominatimGate.WaitAsync(ct);
        try
        {
            var wait = TimeSpan.FromSeconds(1) - (_clock.GetUtcNow() - _lastNominatimCallUtc);
            if (wait > TimeSpan.Zero) await Task.Delay(wait, _clock, ct);

            // Keep the gate held for the whole provider call and stamp the clock only
            // after the response starts. HttpClient can invoke its handler well after
            // SendAsync is called, so stamping earlier can leave real requests less
            // than one second apart. This is conservative but safe for Nominatim.
            try
            {
                return await client.SendAsync(request, ct);
            }
            finally
            {
                _lastNominatimCallUtc = _clock.GetUtcNow();
            }
        }
        finally
        {
            NominatimGate.Release();
        }
    }

    private static bool Valid(double lat, double lng) =>
        double.IsFinite(lat) && double.IsFinite(lng) && lat is >= -90 and <= 90 && lng is >= -180 and <= 180;
}
