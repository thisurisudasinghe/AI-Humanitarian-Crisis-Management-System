namespace Cms.Domain.Operations;
public static class GeoDistance
{
    public static double HaversineKm(double lat1,double lon1,double lat2,double lon2)
    {
        if(!double.IsFinite(lat1)||!double.IsFinite(lon1)||!double.IsFinite(lat2)||!double.IsFinite(lon2)||
           lat1 is <-90 or >90||lat2 is <-90 or >90||lon1 is <-180 or >180||lon2 is <-180 or >180)
            throw new ArgumentOutOfRangeException(nameof(lat1),"Coordinates must be finite and within latitude/longitude bounds.");
        const double radius=6371.0088;
        static double Rad(double value)=>value*Math.PI/180;
        var dLat=Rad(lat2-lat1);var dLon=Rad(lon2-lon1);
        var a=Math.Pow(Math.Sin(dLat/2),2)+Math.Cos(Rad(lat1))*Math.Cos(Rad(lat2))*Math.Pow(Math.Sin(dLon/2),2);
        return radius*2*Math.Asin(Math.Min(1,Math.Sqrt(a)));
    }
}
