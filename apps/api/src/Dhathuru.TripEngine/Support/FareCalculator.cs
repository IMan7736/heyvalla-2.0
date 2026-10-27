using Dhathuru.Domain.Pricing;
using Dhathuru.Domain.Transport;
using Dhathuru.Domain.Trips;
using Dhathuru.TripEngine.Configuration;
using Dhathuru.TripEngine.Network;

namespace Dhathuru.TripEngine.Support;

/// <summary>Price of one hop for the whole party, in minor units. Taxis are per vehicle; walking is free.</summary>
internal sealed class FareCalculator(EngineOptions options)
{
    public long PartyFare(Hop h, Passengers pax)
    {
        var fare = h.Fare.AmountMinor;
        if (h.Mode == TransportMode.Taxi)
        {
            var vehicles = (int)Math.Ceiling(pax.Total / (double)options.Taxi.VehicleCapacity);
            return fare * Math.Max(1, vehicles);
        }
        if (h.Mode == TransportMode.Walk) return 0;
        return fare * pax.Adults
            + CeilPercent(fare, options.Pricing.ChildFarePercent) * pax.Children
            + CeilPercent(fare, options.Pricing.InfantFarePercent) * pax.Infants;
    }

    private static long CeilPercent(long amount, int percent) => (amount * percent + 99) / 100;

    public static string BreakdownCode(TransportMode mode) => mode switch
    {
        TransportMode.Ferry => "FERRY_FARE",
        TransportMode.Speedboat => "SPEEDBOAT_FARE",
        TransportMode.Seaplane => "SEAPLANE_FARE",
        TransportMode.DomesticFlight => "FLIGHT_FARE",
        TransportMode.Bus => "BUS_FARE",
        TransportMode.Taxi => "TAXI_FARE",
        TransportMode.ResortTransfer => "RESORT_TRANSFER_FARE",
        _ => "WALK",
    };
}
