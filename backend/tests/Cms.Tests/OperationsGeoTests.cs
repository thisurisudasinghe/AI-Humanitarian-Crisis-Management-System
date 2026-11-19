using Cms.Domain.Operations;
namespace Cms.Tests;
public class OperationsGeoTests
{
    [Fact]
    public void Haversine_ReturnsZero_ForSamePoint() =>
        Assert.Equal(0,GeoDistance.HaversineKm(7.2088,79.8358,7.2088,79.8358),6);

    [Fact]
    public void Haversine_ReturnsExpectedApproximateDistance_ForNegomboToColombo()
    {
        var km=GeoDistance.HaversineKm(7.2088,79.8358,6.9271,79.8612);
        Assert.InRange(km,30,33);
    }

    [Fact]
    public void Haversine_RejectsInvalidCoordinates() =>
        Assert.Throws<ArgumentOutOfRangeException>(()=>GeoDistance.HaversineKm(100,0,0,0));
}
