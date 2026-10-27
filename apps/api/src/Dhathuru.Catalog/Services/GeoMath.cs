using Dhathuru.Domain.Places;

namespace Dhathuru.Catalog.Services;

public static class GeoMath
{
    private const double EarthRadiusKm = 6371.0088;

    public static double HaversineKm(GeoPoint a, GeoPoint b)
    {
        var dLat = ToRad(b.Lat - a.Lat);
        var dLng = ToRad(b.Lng - a.Lng);
        var h = Math.Pow(Math.Sin(dLat / 2), 2) + Math.Cos(ToRad(a.Lat)) * Math.Cos(ToRad(b.Lat)) * Math.Pow(Math.Sin(dLng / 2), 2);
        return 2 * EarthRadiusKm * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    private static double ToRad(double deg) => deg * Math.PI / 180;
}
