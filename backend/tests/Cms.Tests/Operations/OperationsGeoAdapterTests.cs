using System.Net;
using Cms.Api.Modules;
using Cms.Application.Contracts;
using Cms.Infrastructure.Operations;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using GeoPoint = Cms.Application.Contracts.Operations.GeoPoint;

namespace Cms.Tests.Operations;

/// <summary>D4-12 item 2/fallback: GeoService over fake HttpMessageHandlers, no network.</summary>
public class OperationsGeoAdapterTests
{
    [Fact]
    public async Task Geocode_NominatimDown_ReturnsNullAndCachesTheFailure()
    {
        var (geo, nominatim, osrm) = CreateService(
            _ => throw new HttpRequestException("nominatim offline"),
            _ => throw new InvalidOperationException("osrm must not be used"));

        var first = await geo.GeocodeAsync("Colombo Fort", CancellationToken.None);
        var second = await geo.GeocodeAsync("Colombo Fort", CancellationToken.None);

        Assert.Null(first);
        Assert.Null(second);
        Assert.Equal(1, nominatim.CallCount);
        Assert.Equal(0, osrm.CallCount);
    }

    [Fact]
    public async Task Geocode_Success_IsCachedUnderNormalizedAddress()
    {
        var response = "[{\"lat\":\"6.9271\",\"lon\":\"79.8612\",\"display_name\":\"Colombo, Sri Lanka\"}]";
        var (geo, nominatim, _) = CreateService(
            _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(response) },
            _ => throw new InvalidOperationException("osrm must not be used"));

        var first = await geo.GeocodeAsync("  Colombo   Fort  ", CancellationToken.None);
        var second = await geo.GeocodeAsync("colombo fort", CancellationToken.None);

        Assert.NotNull(first);
        Assert.Equal(first, second);
        Assert.Equal(6.9271, first!.Lat);
        Assert.Equal(79.8612, first.Lng);
        Assert.Equal(1, nominatim.CallCount);
        Assert.Contains("q=Colombo%20Fort", Assert.Single(nominatim.Requests).Uri);
        Assert.Contains("format=jsonv2", Assert.Single(nominatim.Requests).Uri);
    }

    [Fact]
    public async Task GetDistance_OsrmDown_FallsBackToHaversineWithNullMinutes()
    {
        var (geo, nominatim, osrm) = CreateService(
            _ => throw new InvalidOperationException("nominatim must not be used"),
            _ => throw new HttpRequestException("osrm offline"));
        var from = new GeoPoint(51.5074, -0.1278);
        var to = new GeoPoint(48.8566, 2.3522);

        var first = await geo.GetDistanceAsync(from, to, CancellationToken.None);
        var second = await geo.GetDistanceAsync(from, to, CancellationToken.None);

        Assert.Equal(Math.Round(GeoService.HaversineKm(from, to), 2), first.Km);
        Assert.Null(first.Minutes);
        Assert.True(first.IsApproximate);
        Assert.Equal(first, second);
        Assert.Equal(0, nominatim.CallCount);
        Assert.Equal(1, osrm.CallCount); // fallback result is cached too
    }

    [Fact]
    public async Task GetDistance_OsrmSuccess_IsPreciseAndCached()
    {
        var json = "{\"routes\":[{\"distance\":343000,\"duration\":20640}]}";
        var (geo, nominatim, osrm) = CreateService(
            _ => throw new InvalidOperationException("nominatim must not be used"),
            _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        var from = new GeoPoint(51.5074, -0.1278);
        var to = new GeoPoint(48.8566, 2.3522);

        var first = await geo.GetDistanceAsync(from, to, CancellationToken.None);
        var second = await geo.GetDistanceAsync(from, to, CancellationToken.None);

        Assert.Equal(343d, first.Km);
        Assert.Equal(344d, first.Minutes);
        Assert.False(first.IsApproximate);
        Assert.Equal(first, second);
        Assert.Equal(1, osrm.CallCount);
        Assert.Equal(0, nominatim.CallCount);
        Assert.Contains("route/v1/driving", Assert.Single(osrm.Requests).Uri);
    }

    [Fact]
    public void Haversine_LondonToParis_IsAbout343Km()
    {
        var km = GeoService.HaversineKm(51.5074, -0.1278, 48.8566, 2.3522);

        Assert.InRange(km, 340d, 347d);
    }

    [Fact]
    public async Task GetDistance_InvalidCoordinates_ThrowsWithoutCallingAnyProvider()
    {
        var (geo, nominatim, osrm) = CreateService(
            _ => throw new InvalidOperationException("nominatim must not be used"),
            _ => throw new InvalidOperationException("osrm must not be used"));

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            geo.GetDistanceAsync(new GeoPoint(91, 0), new GeoPoint(0, 0), CancellationToken.None));

        Assert.Equal(0, nominatim.CallCount);
        Assert.Equal(0, osrm.CallCount);
    }

    [Fact]
    public async Task Geocode_NominatimClient_UsesTheConfiguredUserAgent()
    {
        var json = "[{\"lat\":\"6.9\",\"lon\":\"79.8\",\"display_name\":\"Colombo\"}]";
        var handler = new RecordingHandler(
            _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        var services = new ServiceCollection();
        services.AddLogisticsModule();
        services.AddHttpClient("nominatim").ConfigurePrimaryHttpMessageHandler(() => handler);
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var geo = scope.ServiceProvider
            .GetRequiredService<Cms.Application.Contracts.Operations.IGeoService>();

        var point = await geo.GeocodeAsync("Colombo Fort", CancellationToken.None);

        Assert.NotNull(point);
        Assert.Equal(1, handler.CallCount);
        Assert.Contains("CMS-Humanitarian/1.0", Assert.Single(handler.Requests).UserAgent);
    }

    private static (GeoService Service, RecordingHandler Nominatim, RecordingHandler Osrm) CreateService(
        Func<HttpRequestMessage, HttpResponseMessage> nominatim,
        Func<HttpRequestMessage, HttpResponseMessage> osrm)
    {
        var nominatimHandler = new RecordingHandler(nominatim);
        var osrmHandler = new RecordingHandler(osrm);
        var factory = new FakeClientFactory(nominatimHandler, osrmHandler);
        var cache = new MemoryCache(new MemoryCacheOptions());
        return (new GeoService(factory, cache, TimeProvider.System), nominatimHandler, osrmHandler);
    }
}

internal sealed record CapturedGeoRequest(string Uri, string UserAgent, string AcceptLanguage);

internal sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public int CallCount { get; private set; }

    public List<CapturedGeoRequest> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        CallCount++;
        Requests.Add(new CapturedGeoRequest(request.RequestUri?.AbsoluteUri ?? string.Empty,
            request.Headers.UserAgent.ToString(), request.Headers.AcceptLanguage.ToString()));
        return Task.FromResult(respond(request));
    }
}

internal sealed class FakeClientFactory : IHttpClientFactory
{
    private readonly Dictionary<string, HttpClient> _clients;

    public FakeClientFactory(RecordingHandler nominatim, RecordingHandler osrm)
    {
        _clients = new Dictionary<string, HttpClient>(StringComparer.OrdinalIgnoreCase)
        {
            ["nominatim"] = new HttpClient(nominatim) { BaseAddress = new Uri("https://nominatim.openstreetmap.org/") },
            ["osrm"] = new HttpClient(osrm) { BaseAddress = new Uri("https://router.project-osrm.org/") }
        };
    }

    public HttpClient CreateClient(string name) => _clients.TryGetValue(name, out var client)
        ? client
        : throw new InvalidOperationException($"Unexpected HTTP client '{name}'.");
}
