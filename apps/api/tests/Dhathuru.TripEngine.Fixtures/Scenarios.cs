using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Places;
using Dhathuru.Domain.Transport;

namespace Dhathuru.TripEngine.Fixtures;

/// <summary>
/// A small hand-made slice of the country: Maafushi to Kooddoo through Hulhumale and Velana airport
/// (the acceptance-demo journey: ferry, taxi, flight, taxi). All times and fares are invented.
/// </summary>
public sealed class MiniMaldives
{
    public const string Today = "2026-10-27";
    public const string Tomorrow = "2026-10-28";

    public WorldBuilder World { get; } = new();

    public PlaceId Maafushi { get; }
    public PlaceId MaafushiJetty { get; }
    public PlaceId Terminal { get; }       // Hulhumale ferry terminal
    public PlaceId Velana { get; }         // MLE
    public PlaceId KooddooAirport { get; } // GKK
    public PlaceId Kooddoo { get; }
    public PlaceId Gan { get; }
    public PlaceId GanAirport { get; }
    public PlaceId Island { get; }         // an island with no services at all

    public MiniMaldives(bool includeTomorrow = true)
    {
        var w = World;
        Maafushi = w.Place("Maafushi", PlaceType.Island, lat: 3.94, lng: 73.49);
        MaafushiJetty = w.Place("Maafushi Jetty", PlaceType.Jetty, Maafushi, 3.94, 73.49);
        Terminal = w.Place("Hulhumale Ferry Terminal", PlaceType.Jetty, null, 4.22, 73.54, hub: true);
        Velana = w.Place("Velana International Airport", PlaceType.Airport, null, 4.19, 73.53, hub: true);
        Kooddoo = w.Place("Kooddoo", PlaceType.Island, lat: 0.73, lng: 73.43);
        KooddooAirport = w.Place("Kooddoo Airport", PlaceType.Airport, Kooddoo, 0.73, 73.43);
        Gan = w.Place("Gan", PlaceType.Island, lat: -0.69, lng: 73.15);
        GanAirport = w.Place("Gan Airport", PlaceType.Airport, Gan, -0.69, 73.15);
        Island = w.Place("Lonely Island", PlaceType.Island, lat: 5.5, lng: 73.0);

        w.Link(Terminal, Velana, TransportMode.Taxi, 15, 80);

        var days = includeTomorrow ? new[] { Today, Tomorrow } : new[] { Today };
        // Boats Maafushi <-> Hulhumale terminal.
        foreach (var d in days)
        {
            w.Run("ferry-maafushi", TransportMode.Ferry, MaafushiJetty, Terminal, $"{d} 07:30", 90, 90);
            w.Run("ferry-maafushi", TransportMode.Ferry, MaafushiJetty, Terminal, $"{d} 14:00", 90, 90);
            w.Run("ferry-maafushi", TransportMode.Ferry, MaafushiJetty, Terminal, $"{d} 16:30", 90, 90);
            w.Run("speedboat-maafushi", TransportMode.Speedboat, MaafushiJetty, Terminal, $"{d} 09:00", 45, 300);
            w.Run("speedboat-maafushi", TransportMode.Speedboat, MaafushiJetty, Terminal, $"{d} 11:00", 45, 300);
            w.Run("ferry-maafushi", TransportMode.Ferry, Terminal, MaafushiJetty, $"{d} 15:00", 90, 90);
        }
        // Domestic flights MLE -> GKK and MLE -> GAN.
        foreach (var d in days)
        {
            w.Run("air-gkk", TransportMode.DomesticFlight, Velana, KooddooAirport, $"{d} 09:30", 65, 1850, 45);
            w.Run("air-gkk", TransportMode.DomesticFlight, Velana, KooddooAirport, $"{d} 13:00", 65, 1850, 45);
            w.Run("air-gkk", TransportMode.DomesticFlight, Velana, KooddooAirport, $"{d} 16:00", 65, 1850, 45);
            w.Run("air-gan", TransportMode.DomesticFlight, Velana, GanAirport, $"{d} 10:00", 85, 2400, 45);
            w.Run("air-gkk-back", TransportMode.DomesticFlight, KooddooAirport, Velana, $"{d} 10:45", 65, 1850, 45);
        }
    }

    public StaticSnapshotProvider Provider() => new(World.Build());
}
