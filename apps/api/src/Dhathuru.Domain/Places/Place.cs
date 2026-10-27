using Dhathuru.Domain.Ids;

namespace Dhathuru.Domain.Places;

public enum PlaceType { Island, Atoll, Airport, Jetty, TaxiStand, Poi, Address }

public readonly record struct GeoPoint
{
    public GeoPoint(double lat, double lng)
    {
        if (lat is < -90 or > 90) throw new ArgumentOutOfRangeException(nameof(lat));
        if (lng is < -180 or > 180) throw new ArgumentOutOfRangeException(nameof(lng));
        Lat = lat;
        Lng = lng;
    }

    public double Lat { get; }

    public double Lng { get; }
}

/// <summary>Lightweight place reference embedded in legs. Names are localised: locale tag to text, <c>en</c> always present.</summary>
public sealed record PlaceRef(
    PlaceId Id,
    PlaceType Type,
    IReadOnlyDictionary<string, string> Name,
    GeoPoint Geo,
    string? Code = null);
