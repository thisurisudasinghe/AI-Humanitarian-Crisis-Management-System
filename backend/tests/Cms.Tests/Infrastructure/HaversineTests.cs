using Cms.Application.Contracts;
using Cms.Infrastructure.Stubs;

namespace Cms.Tests.Infrastructure;

public class HaversineTests
{
    [Fact]
    public async Task Distance_between_identical_points_is_zero()
    {
        var service = new HaversineOnlyGeoService();
        var point = new GeoPoint(6.9271, 79.8612);

        var result = await service.GetDistanceAsync(point, point);

        Assert.Equal(0d, result.Km, precision: 6);
        Assert.True(result.IsApproximate);
        Assert.Null(result.Minutes);
    }

    [Fact]
    public async Task One_degree_of_longitude_at_the_equator_is_about_111_km()
    {
        var service = new HaversineOnlyGeoService();

        var result = await service.GetDistanceAsync(new GeoPoint(0, 0), new GeoPoint(0, 1));

        Assert.InRange(result.Km, 111.0, 111.4);
        Assert.True(result.IsApproximate);
    }

    [Fact]
    public async Task Colombo_to_Kandy_is_about_95_km()
    {
        var service = new HaversineOnlyGeoService();

        var result = await service.GetDistanceAsync(
            new GeoPoint(6.9271, 79.8612),
            new GeoPoint(7.2906, 80.6337));

        Assert.InRange(result.Km, 85d, 105d);
    }

    [Fact]
    public async Task Geocoding_is_unavailable_in_the_stub()
    {
        var service = new HaversineOnlyGeoService();

        Assert.Null(await service.GeocodeAsync("Colombo Fort"));
    }
}
