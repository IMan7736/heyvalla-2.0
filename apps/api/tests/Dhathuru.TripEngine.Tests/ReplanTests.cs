using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Transport;
using Dhathuru.Domain.Trips;
using Dhathuru.TripEngine.Fixtures;
using Dhathuru.Domain.Seams;

namespace Dhathuru.TripEngine.Tests;

public class ReplanTests
{
    private static readonly MiniMaldives W = new();

    /// <summary>Books the 07:30 ferry, taxi, 13:00 flight, walk journey, as the booking module would hold it.</summary>
    private static async Task<(TripPlanner Planner, BookingId Id, TripOption Booked)> Setup()
    {
        var id = BookingId.New();
        var probe = H.Planner(W.World);
        var plan = await probe.PlanAsync(H.Q(W.Maafushi, W.Kooddoo, "2026-10-27", "05:00"), H.Ct);
        var booked = plan.Options.First(o => H.Uses(o, "07:30", TransportMode.Ferry) && H.Uses(o, "13:00", TransportMode.DomesticFlight));
        var bookings = new Dictionary<BookingId, BookedItinerary>
        {
            [id] = new(booked.Legs, new Passengers(1), new TripPreferences(RiskTolerance.Balanced)),
        };
        return (H.Planner(W.World, bookings), id, booked);
    }

    [Fact]
    public async Task A_delay_the_connections_absorb_returns_the_same_trip_with_updated_buffers()
    {
        var (planner, id, booked) = await Setup();
        var ferry = booked.Legs.First(l => l.Mode == TransportMode.Ferry);
        var plan = await planner.PlanReplanAsync(
            new ReplanQuery(id, H.At("2026-10-27 07:45"), new Disruption(ferry.Id, DisruptionKind.Delayed, 40)), H.Ct);

        var option = plan.Options[0];
        H.AssertSound(option);
        Assert.Equal(booked.Legs.Select(l => l.Id), option.Legs.Select(l => l.Id));
        Assert.Equal(booked.Legs.Select(l => l.ServiceRunId), option.Legs.Select(l => l.ServiceRunId));
        Assert.Equal(40, (int)(option.Legs[1].ArriveAt - booked.Legs[1].ArriveAt).TotalMinutes);
        var absorbed = option.Explanations.Single(e => e.Code == "DELAY_ABSORBED");
        Assert.Equal(40, absorbed.Params["minutes"]);
        Assert.Contains(option.Explanations, e => e.Code == "DISRUPTION_DELAYED");
        Assert.Single(plan.Options);
    }

    [Fact]
    public async Task A_cancelled_ferry_is_replaced_by_a_boat_that_still_makes_the_confirmed_flight()
    {
        var (planner, id, booked) = await Setup();
        var ferry = booked.Legs.First(l => l.Mode == TransportMode.Ferry);
        var flight = booked.Legs.Single(l => l.Mode == TransportMode.DomesticFlight);

        var plan = await planner.PlanReplanAsync(
            new ReplanQuery(id, H.At("2026-10-27 07:00"), new Disruption(ferry.Id, DisruptionKind.Cancelled)), H.Ct);

        Assert.NotEmpty(plan.Options);
        Assert.All(plan.Options, H.AssertSound);
        Assert.All(plan.Options, o => Assert.DoesNotContain(o.Legs, l => l.ServiceRunId == ferry.ServiceRunId));
        var keeps = plan.Options.First();
        Assert.Contains(keeps.Legs, l => l.Id == flight.Id); // the booked flight is preserved with its original leg id
        Assert.True(keeps.Explanations.Any(e => e.Code == "REPLAN_KEEPS_LEGS"), string.Join(",", keeps.Explanations.Select(e => e.Code)));
        Assert.Contains(keeps.Legs, l => l.Mode == TransportMode.Speedboat);
        Assert.Contains(keeps.Explanations, e => e.Code == "DISRUPTION_CANCELLED");
    }

    [Fact]
    public async Task A_delay_that_breaks_the_flight_connection_finds_a_later_safe_flight()
    {
        var (planner, id, booked) = await Setup();
        var ferry = booked.Legs.First(l => l.Mode == TransportMode.Ferry);
        var flight = booked.Legs.Single(l => l.Mode == TransportMode.DomesticFlight);

        var plan = await planner.PlanReplanAsync(
            new ReplanQuery(id, H.At("2026-10-27 08:00"), new Disruption(ferry.Id, DisruptionKind.Delayed, 200)), H.Ct);

        Assert.NotEmpty(plan.Options);
        Assert.All(plan.Options, H.AssertSound);
        Assert.All(plan.Options, o => Assert.DoesNotContain(o.Legs, l => l.Id == flight.Id));
        Assert.Contains(plan.Options, o => H.Uses(o, "16:00", TransportMode.DomesticFlight));
        // The delayed ferry itself stays as it was booked (same id, later arrival).
        Assert.All(plan.Options, o => Assert.Equal(ferry.Id, o.Legs.First(l => l.Mode == TransportMode.Ferry).Id));
    }

    [Fact]
    public async Task Replan_never_proposes_a_time_before_now()
    {
        var (planner, id, booked) = await Setup();
        var ferry = booked.Legs.First(l => l.Mode == TransportMode.Ferry);
        var now = H.At("2026-10-27 10:00");
        var plan = await planner.PlanReplanAsync(
            new ReplanQuery(id, now, new Disruption(ferry.Id, DisruptionKind.Cancelled)), H.Ct);
        foreach (var o in plan.Options)
        {
            // legs that are new (not the booked ones) must start at or after now
            var bookedIds = booked.Legs.Select(l => l.Id).ToHashSet();
            Assert.All(o.Legs.Where(l => !bookedIds.Contains(l.Id)), l => Assert.True(l.DepartAt >= now));
        }
    }

    [Fact]
    public async Task Unknown_booking_or_leg_return_codes_not_exceptions()
    {
        var (planner, id, booked) = await Setup();
        var missing = await planner.PlanReplanAsync(
            new ReplanQuery(BookingId.New(), H.At("2026-10-27 07:00"), new Disruption(booked.Legs[0].Id, DisruptionKind.Cancelled)), H.Ct);
        Assert.Contains(missing.Notices, n => n.Code == "BOOKING_NOT_FOUND");

        var wrongLeg = await planner.PlanReplanAsync(
            new ReplanQuery(id, H.At("2026-10-27 07:00"), new Disruption(LegId.New(), DisruptionKind.Cancelled)), H.Ct);
        Assert.Contains(wrongLeg.Notices, n => n.Code == "DISRUPTED_LEG_NOT_IN_BOOKING");

        var noMinutes = await planner.PlanReplanAsync(
            new ReplanQuery(id, H.At("2026-10-27 07:00"), new Disruption(booked.Legs[1].Id, DisruptionKind.Delayed)), H.Ct);
        Assert.Contains(noMinutes.Notices, n => n.Code == "DELAY_MINUTES_REQUIRED");
    }

    [Fact]
    public async Task Nothing_safe_left_is_said_plainly()
    {
        var w = new WorldBuilder();
        var island = w.Place("A", Dhathuru.Domain.Places.PlaceType.Island);
        var jetty = w.Place("A Jetty", Dhathuru.Domain.Places.PlaceType.Jetty, island);
        var terminal = w.Place("Terminal", Dhathuru.Domain.Places.PlaceType.Jetty);
        var mle = w.Place("MLE", Dhathuru.Domain.Places.PlaceType.Airport);
        var gkk = w.Place("GKK", Dhathuru.Domain.Places.PlaceType.Airport);
        w.Link(terminal, mle, TransportMode.Taxi, 15, 80);
        w.Run("ferry", TransportMode.Ferry, jetty, terminal, "2026-10-27 07:30", 90, 90);
        w.Run("air", TransportMode.DomesticFlight, mle, gkk, "2026-10-27 13:00", 60, 1500, 45);

        var plan = await H.Planner(w).PlanAsync(H.Q(island, gkk, "2026-10-27", "05:00"), H.Ct);
        var option = Assert.Single(plan.Options);
        var bid = BookingId.New();
        var bookings = new Dictionary<BookingId, BookedItinerary> { [bid] = new(option.Legs, new Passengers(1), new TripPreferences(RiskTolerance.Balanced)) };
        var flight = option.Legs.Single(l => l.Mode == TransportMode.DomesticFlight);

        var result = await H.Planner(w, bookings).PlanReplanAsync(
            new ReplanQuery(bid, H.At("2026-10-27 11:00"), new Disruption(flight.Id, DisruptionKind.Cancelled)), H.Ct);

        Assert.Empty(result.Options);
        Assert.Contains(result.Notices, n => n.Code == "NO_SAFE_REPLAN");
        Assert.Contains(result.Notices, n => n.Code == "SUGGEST_CONTACT_OPERATOR");
    }
}
