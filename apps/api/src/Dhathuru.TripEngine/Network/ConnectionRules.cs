using Dhathuru.Domain.Transport;
using Dhathuru.TripEngine.Configuration;

namespace Dhathuru.TripEngine.Network;

/// <summary>
/// The one place that knows how many minutes a connection needs. Router, assembler and replanner all ask here,
/// so the search and the final safety re-check can never disagree.
/// </summary>
internal sealed class ConnectionRules
{
    private readonly ConnectionOptions connections;
    private readonly TaxiOptions taxi;
    private readonly int passengerExtra;

    public ConnectionRules(EngineOptions options, int passengers)
    {
        connections = options.Connections;
        taxi = options.Taxi;
        passengerExtra = Math.Max(0, passengers - 2) * connections.PerExtraPassengerMin;
    }

    /// <summary>
    /// Minimum minutes between arriving on <paramref name="prev"/> and departing on <paramref name="next"/>, in real order.
    /// <c>prev == null</c>: the traveller starts at the place. <c>next == null</c>: the trip ends (deadline), nothing more is needed.
    /// </summary>
    public int Required(Hop? prev, Hop? next)
    {
        if (next is null) return 0;
        var boarding = Boarding(next);
        var core = next.Mode == TransportMode.Taxi
            ? Math.Max(prev is null ? 0 : Alight(prev), boarding)
            : (prev is null ? 0 : Alight(prev)) + boarding;
        var hub = connections.ExtraMinutesByPlace.TryGetValue(next.From.Value, out var extra) ? extra : 0;
        return core + passengerExtra + hub;
    }

    /// <summary>Check-in cutoff or boarding closure that applies to the departing leg.</summary>
    public int Boarding(Hop next) => next.Mode switch
    {
        TransportMode.DomesticFlight => next.CheckInCutoffMin > 0 ? next.CheckInCutoffMin : connections.FlightCheckInDefaultMin,
        TransportMode.Seaplane => next.CheckInCutoffMin > 0 ? next.CheckInCutoffMin : connections.SeaplaneCheckInDefaultMin,
        TransportMode.Ferry or TransportMode.Speedboat or TransportMode.ResortTransfer => connections.BoatBoardingClosureMin,
        TransportMode.Bus => connections.BusBoardingMin,
        TransportMode.Taxi => taxi.PickupLeadMin,
        _ => 0,
    };

    public int Alight(Hop prev) => prev.Mode switch
    {
        TransportMode.DomesticFlight => connections.AlightFlightMin,
        TransportMode.Seaplane => connections.AlightSeaplaneMin,
        TransportMode.Ferry or TransportMode.Speedboat or TransportMode.ResortTransfer => connections.AlightBoatMin,
        _ => connections.AlightOtherMin,
    };

    /// <summary>Taxis only run inside the configured local service window.</summary>
    public bool TaxiAvailable(long depMinute, long arrMinute)
    {
        var start = taxi.ServiceStartMinuteOfDay;
        var end = taxi.ServiceEndMinuteOfDay;
        return Within(TimeMath.LocalMinuteOfDay(depMinute)) && Within(TimeMath.LocalMinuteOfDay(arrMinute));

        bool Within(int m) => m >= start && m <= end;
    }

    /// <summary>Earliest departure at or after <paramref name="dep"/> for which the whole ride fits the taxi window, or null.</summary>
    public long? NextTaxiDeparture(long dep, int durationMin)
    {
        var start = taxi.ServiceStartMinuteOfDay;
        var end = taxi.ServiceEndMinuteOfDay;
        if (durationMin > end - start) return null;
        for (var i = 0; i < 3; i++)
        {
            var m = TimeMath.LocalMinuteOfDay(dep);
            if (m >= start && m + durationMin <= end) return dep;
            dep += m < start ? start - m : 1440 - m + start;
        }
        return null;
    }

    /// <summary>Latest arrival at or before <paramref name="arr"/> for which the whole ride fits the taxi window, or null.</summary>
    public long? PrevTaxiArrival(long arr, int durationMin)
    {
        var start = taxi.ServiceStartMinuteOfDay;
        var end = taxi.ServiceEndMinuteOfDay;
        if (durationMin > end - start) return null;
        for (var i = 0; i < 3; i++)
        {
            var m = TimeMath.LocalMinuteOfDay(arr);
            if (m <= end && m - durationMin >= start) return arr;
            arr -= m > end ? m - end : m + 1440 - end;
        }
        return null;
    }
}
