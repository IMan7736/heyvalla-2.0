using Dhathuru.Catalog.Services;
using Dhathuru.Domain.Places;
using Dhathuru.Domain.Transport;

namespace Dhathuru.Catalog.Tests;

public class RouteGeometryTests
{
    private static readonly GeoPoint Male = new(4.1796, 73.5113);
    private static readonly GeoPoint Maafushi = new(3.9425, 73.4900);

    [Theory]
    [InlineData(TransportMode.Ferry, "sea", RouteGeometry.SeaPoints)]
    [InlineData(TransportMode.Speedboat, "sea", RouteGeometry.SeaPoints)]
    [InlineData(TransportMode.DomesticFlight, "air", RouteGeometry.AirPoints)]
    [InlineData(TransportMode.Taxi, "road", RouteGeometry.RoadPoints)]
    [InlineData(TransportMode.Walk, "walk", 2)]
    public void Each_mode_gets_its_own_style_and_density(TransportMode mode, string kind, int points)
    {
        var g = RouteGeometry.Build(Male, Maafushi, mode);
        Assert.Equal(kind, g.Kind);
        Assert.Equal(points, g.Coordinates.Count);
    }

    [Fact]
    public void Line_starts_and_ends_exactly_on_the_places_in_lng_lat_order()
    {
        var g = RouteGeometry.Build(Male, Maafushi, TransportMode.Speedboat);
        Assert.Equal([Male.Lng, Male.Lat], g.Coordinates[0]);
        Assert.Equal([Maafushi.Lng, Maafushi.Lat], g.Coordinates[^1]);
    }

    [Fact]
    public void Geometry_is_always_flagged_approximate()
    {
        foreach (var mode in Enum.GetValues<TransportMode>())
            Assert.True(RouteGeometry.Build(Male, Maafushi, mode).Approximate);
    }

    [Fact]
    public void Sea_leg_bends_away_from_the_straight_line_but_stays_close()
    {
        var g = RouteGeometry.Build(Male, Maafushi, TransportMode.Ferry);
        var mid = g.Coordinates[g.Coordinates.Count / 2];
        var straightMidLng = (Male.Lng + Maafushi.Lng) / 2;
        var offset = Math.Abs(mid[0] - straightMidLng);
        Assert.InRange(offset, 0.005, 0.1);
    }

    [Fact]
    public void Reversing_the_leg_bends_to_the_other_side_so_return_trips_do_not_overlap()
    {
        var there = RouteGeometry.Build(Male, Maafushi, TransportMode.Ferry).Coordinates[SeaMid];
        var back = RouteGeometry.Build(Maafushi, Male, TransportMode.Ferry).Coordinates[SeaMid];
        Assert.NotEqual(there[0], back[0], 4);
    }

    private const int SeaMid = RouteGeometry.SeaPoints / 2;

    [Fact]
    public void Same_place_and_repeat_calls_are_safe_and_deterministic()
    {
        var same = RouteGeometry.Build(Male, Male, TransportMode.Ferry);
        Assert.Equal(2, same.Coordinates.Count);
        var a = RouteGeometry.Build(Male, Maafushi, TransportMode.Taxi);
        var b = RouteGeometry.Build(Male, Maafushi, TransportMode.Taxi);
        Assert.Equal(a.Coordinates.SelectMany(c => c), b.Coordinates.SelectMany(c => c));
    }

    [Fact]
    public void All_coordinates_are_valid_longitude_latitude()
    {
        var g = RouteGeometry.Build(new GeoPoint(-0.69, 73.15), new GeoPoint(6.74, 73.17), TransportMode.DomesticFlight);
        Assert.All(g.Coordinates, c =>
        {
            Assert.InRange(c[0], -180, 180);
            Assert.InRange(c[1], -90, 90);
        });
    }
}
