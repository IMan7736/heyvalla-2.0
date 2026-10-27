using Dhathuru.Domain.Places;
using Dhathuru.Domain.Transport;

namespace Dhathuru.Catalog.Services;

/// <summary>GeoJSON-style line, coordinates as [lng, lat]. Always approximate: it shows the idea of a route, it is not a navigation track.</summary>
public sealed record LegGeometry(string Kind, IReadOnlyList<double[]> Coordinates, bool Approximate = true);

/// <summary>
/// Illustrative geometry between two places for the map. Sea and air legs are drawn as a gentle arc so they read as
/// travel across water, road legs as a slight bend, walking as a straight line. No chart, road or flight-path data is
/// used, so the result must never be presented as navigable. Deterministic: the same inputs give the same line.
/// </summary>
public static class RouteGeometry
{
    public const int SeaPoints = 32;
    public const int AirPoints = 48;
    public const int RoadPoints = 12;

    public static LegGeometry Build(GeoPoint from, GeoPoint to, TransportMode mode)
    {
        var (kind, points, bend) = mode switch
        {
            TransportMode.Ferry or TransportMode.Speedboat or TransportMode.Seaplane or TransportMode.ResortTransfer => ("sea", SeaPoints, 0.12),
            TransportMode.DomesticFlight => ("air", AirPoints, 0.06),
            TransportMode.Taxi or TransportMode.Bus => ("road", RoadPoints, 0.03),
            _ => ("walk", 2, 0.0),
        };

        if (from.Lat == to.Lat && from.Lng == to.Lng) return new LegGeometry(kind, [[from.Lng, from.Lat], [to.Lng, to.Lat]]);
        if (points == 2 || bend == 0) return new LegGeometry(kind, [[from.Lng, from.Lat], [to.Lng, to.Lat]]);

        // Quadratic Bezier whose control point sits off the chord's midpoint, always to the left of travel.
        var dx = to.Lng - from.Lng;
        var dy = to.Lat - from.Lat;
        var cx = (from.Lng + to.Lng) / 2 - dy * bend;
        var cy = (from.Lat + to.Lat) / 2 + dx * bend;
        var coordinates = new List<double[]>(points);
        for (var i = 0; i < points; i++)
        {
            var t = (double)i / (points - 1);
            var u = 1 - t;
            var lng = u * u * from.Lng + 2 * u * t * cx + t * t * to.Lng;
            var lat = u * u * from.Lat + 2 * u * t * cy + t * t * to.Lat;
            coordinates.Add([Math.Round(lng, 6), Math.Round(lat, 6)]);
        }
        coordinates[0] = [from.Lng, from.Lat];
        coordinates[^1] = [to.Lng, to.Lat];
        return new LegGeometry(kind, coordinates);
    }
}
