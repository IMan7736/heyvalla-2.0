using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Places;
using Dhathuru.Domain.Transport;

namespace Dhathuru.TripEngine.Fixtures;

/// <summary>A small random network for property tests. Same seed, same world.</summary>
public sealed class RandomWorld
{
    public WorldBuilder World { get; } = new();

    /// <summary>Places a traveller may start or end at.</summary>
    public List<PlaceId> Endpoints { get; } = [];

    public string[] ServiceNames { get; }

    public RandomWorld(int seed)
    {
        var r = new Random(seed);
        var w = World;
        var services = new List<string>();

        var hubCount = r.Next(1, 3);
        var hubJetties = new List<PlaceId>();
        var hubAirports = new List<PlaceId>();
        for (var h = 0; h < hubCount; h++)
        {
            var jetty = w.Place($"Hub{h} Jetty", PlaceType.Jetty, hub: true);
            var airport = w.Place($"Hub{h} Airport", PlaceType.Airport, hub: true);
            w.Link(jetty, airport, TransportMode.Taxi, r.Next(8, 25), r.Next(40, 150));
            hubJetties.Add(jetty);
            hubAirports.Add(airport);
            Endpoints.Add(jetty);
        }

        var islandCount = r.Next(3, 7);
        var islandJetties = new List<PlaceId>();
        for (var i = 0; i < islandCount; i++)
        {
            var island = w.Place($"Island{i}", PlaceType.Island);
            var jetty = w.Place($"Island{i} Jetty", PlaceType.Jetty, island);
            islandJetties.Add(jetty);
            Endpoints.Add(island);
        }
        var remoteAirports = new List<PlaceId>();
        for (var a = 0; a < r.Next(1, 4); a++)
        {
            var island = w.Place($"Atoll{a}", PlaceType.Island);
            var airport = w.Place($"Atoll{a} Airport", PlaceType.Airport, island);
            remoteAirports.Add(airport);
            Endpoints.Add(island);
        }

        string[] days = ["2026-10-27", "2026-10-28"];
        var svc = 0;
        void Boat(PlaceId from, PlaceId to)
        {
            var name = $"boat{svc++}";
            services.Add(name);
            var mode = r.Next(2) == 0 ? TransportMode.Ferry : TransportMode.Speedboat;
            var dur = r.Next(20, 140);
            var fare = r.Next(30, 400);
            foreach (var d in days)
            {
                for (var k = 0; k < r.Next(1, 5); k++)
                    w.Run(name, mode, from, to, $"{d} {r.Next(5, 22):00}:{r.Next(0, 6) * 10:00}", dur, fare);
            }
        }
        void Flight(PlaceId from, PlaceId to)
        {
            var name = $"air{svc++}";
            services.Add(name);
            var dur = r.Next(35, 100);
            var fare = r.Next(900, 3000);
            var cutoff = r.Next(30, 61);
            foreach (var d in days)
            {
                for (var k = 0; k < r.Next(1, 4); k++)
                    w.Run(name, TransportMode.DomesticFlight, from, to, $"{d} {r.Next(6, 20):00}:{r.Next(0, 6) * 10:00}", dur, fare, cutoff);
            }
        }

        foreach (var ij in islandJetties)
        {
            var hub = hubJetties[r.Next(hubJetties.Count)];
            Boat(ij, hub);
            Boat(hub, ij);
            if (r.Next(3) == 0) Boat(ij, islandJetties[r.Next(islandJetties.Count)] is var other && other != ij ? other : hub);
        }
        foreach (var ra in remoteAirports)
        {
            var hub = hubAirports[r.Next(hubAirports.Count)];
            Flight(hub, ra);
            Flight(ra, hub);
        }
        if (hubAirports.Count > 1)
        {
            Flight(hubAirports[0], hubAirports[1]);
            Flight(hubAirports[1], hubAirports[0]);
        }
        ServiceNames = services.ToArray();
    }
}
