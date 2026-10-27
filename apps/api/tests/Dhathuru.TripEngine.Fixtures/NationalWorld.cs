using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Places;
using Dhathuru.Domain.Transport;

namespace Dhathuru.TripEngine.Fixtures;

/// <summary>
/// A country-sized synthetic network for performance work: about 20 atolls, 190 islands, 14 domestic airports, a Male hub,
/// boats between islands, atoll hubs and Male, and flights from Velana. Names are generic ("Atoll 07 Island 03");
/// timetables are invented. No dataset rows are involved.
/// </summary>
public sealed class NationalWorld
{
    public WorldBuilder World { get; } = new();

    public PlaceId Male { get; }
    public PlaceId Velana { get; }
    public List<PlaceId> Islands { get; } = [];
    public List<PlaceId> AirportIslands { get; } = [];

    public int RunCount { get; private set; }

    /// <param name="density">1 = about 6 000 runs over three days. Higher multiplies departures per route.</param>
    public NationalWorld(int seed = 1, int density = 1)
    {
        var r = new Random(seed);
        var w = World;
        string[] days = ["2026-10-26", "2026-10-27", "2026-10-28"];

        Male = w.Place("Male", PlaceType.Island, lat: 4.175, lng: 73.509);
        var maleJetty = w.Place("Male Jetty", PlaceType.Jetty, Male, 4.175, 73.509, hub: true);
        var terminal = w.Place("Hulhumale Terminal", PlaceType.Jetty, null, 4.22, 73.54, hub: true);
        Velana = w.Place("Velana Airport", PlaceType.Airport, null, 4.19, 73.53, hub: true);
        w.Link(terminal, Velana, TransportMode.Taxi, 15, 80);
        w.Link(maleJetty, terminal, TransportMode.Taxi, 25, 120);
        w.Link(maleJetty, Velana, TransportMode.Taxi, 20, 100);
        Islands.Add(Male);

        var hubJetties = new List<PlaceId>();
        for (var a = 1; a <= 20; a++)
        {
            var lat = 7.0 - a * 0.35;
            var hubIsland = w.Place($"Atoll {a:00} Hub", PlaceType.Island, lat: lat, lng: 73.2);
            var hubJetty = w.Place($"Atoll {a:00} Hub Jetty", PlaceType.Jetty, hubIsland, lat, 73.2, hub: true);
            hubJetties.Add(hubJetty);
            Islands.Add(hubIsland);

            if (a <= 14)
            {
                var airport = w.Place($"Atoll {a:00} Airport", PlaceType.Airport, hubIsland, lat, 73.2);
                AirportIslands.Add(hubIsland);
                // Airports sit on or beside the hub island; a short taxi connects the hub jetty.
                w.Link(hubJetty, airport, TransportMode.Taxi, r.Next(5, 20), r.Next(30, 120));
                foreach (var d in days)
                {
                    var per = r.Next(3, 7) * density;
                    for (var k = 0; k < per; k++)
                    {
                        var t = new TimeOnly(6, 0).AddMinutes(k * (840 / per) + r.Next(0, 20));
                        Add("air-out", TransportMode.DomesticFlight, Velana, airport, d, t, 45 + a * 4, 900 + a * 80, 45);
                        Add("air-back", TransportMode.DomesticFlight, airport, Velana, d, t.AddMinutes(30), 45 + a * 4, 900 + a * 80, 45);
                    }
                }
            }

            var islandCount = r.Next(7, 12);
            for (var i = 1; i <= islandCount; i++)
            {
                var island = w.Place($"Atoll {a:00} Island {i:00}", PlaceType.Island, lat: lat + r.NextDouble() * 0.2, lng: 73.1 + r.NextDouble() * 0.2);
                var jetty = w.Place($"Atoll {a:00} Island {i:00} Jetty", PlaceType.Jetty, island, lat, 73.2);
                Islands.Add(island);
                var minutes = r.Next(25, 150);
                var fare = r.Next(25, 220);
                var mode = r.Next(3) == 0 ? TransportMode.Ferry : TransportMode.Speedboat;
                foreach (var d in days)
                {
                    var per = r.Next(2, 6) * density;
                    for (var k = 0; k < per; k++)
                    {
                        var t = new TimeOnly(5, 30).AddMinutes(k * (900 / per) + r.Next(0, 25));
                        Add($"boat-{a}-{i}-out", mode, jetty, hubJetty, d, t, minutes, fare);
                        Add($"boat-{a}-{i}-in", mode, hubJetty, jetty, d, t.AddMinutes(20), minutes, fare);
                    }
                    // Some islands also have a direct boat to Male.
                    if (r.Next(10) < 3)
                    {
                        var direct = r.Next(1, 3) * density;
                        for (var k = 0; k < direct; k++)
                        {
                            var t = new TimeOnly(7, 0).AddMinutes(k * 300 + r.Next(0, 30));
                            Add($"male-{a}-{i}-out", TransportMode.Speedboat, jetty, terminal, d, t, minutes + 60, fare + 150);
                            Add($"male-{a}-{i}-in", TransportMode.Speedboat, terminal, jetty, d, t.AddMinutes(150), minutes + 60, fare + 150);
                        }
                    }
                }
            }
            // The hub itself connects to Male by a daily ferry.
            foreach (var d in days)
            {
                Add($"hub-{a}-male-out", TransportMode.Ferry, hubJetty, terminal, d, new TimeOnly(8, 0), 300 + a * 10, 200);
                Add($"hub-{a}-male-in", TransportMode.Ferry, terminal, hubJetty, d, new TimeOnly(15, 0), 300 + a * 10, 200);
            }
        }

        void Add(string service, TransportMode mode, PlaceId from, PlaceId to, string day, TimeOnly t, int minutes, long fare, int cutoff = 0)
        {
            var date = DateOnly.Parse(day);
            World.Run(service, mode, from, to, $"{date:yyyy-MM-dd} {t:HH:mm}", minutes, fare, cutoff);
            RunCount++;
        }
    }
}
