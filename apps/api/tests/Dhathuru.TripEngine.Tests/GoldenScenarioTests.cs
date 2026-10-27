using Dhathuru.Domain.Transport;
using Dhathuru.Domain.Trips;
using Dhathuru.TripEngine.Fixtures;

namespace Dhathuru.TripEngine.Tests;

/// <summary>
/// Hand-verified routes on the MiniMaldives fixture. Arithmetic (balanced party of one, defaults):
/// walk to jetty 5 min; ferry needs 3+15 = 18 min after a walk; taxi needs max(10, 8) = 10 min after a boat and takes 15;
/// flight needs 3+45 = 48 min after a taxi; the final walk needs 20 min after a flight.
/// So 07:30 ferry arrives 09:00, taxi 09:10-09:25, the 09:30 flight is out of reach (needs 10:13), the 13:00 flight works,
/// lands 14:05, walk 14:25-14:30.
/// </summary>
public class GoldenScenarioTests
{
    private static readonly MiniMaldives W = new();

    [Fact]
    public async Task Depart_after_finds_the_ferry_taxi_flight_walk_journey_and_skips_the_unreachable_09_30_flight()
    {
        var planner = H.Planner(W.World);
        var plan = await planner.PlanAsync(H.Q(W.Maafushi, W.Kooddoo, "2026-10-27", "05:00"), H.Ct);

        Assert.NotEmpty(plan.Options);
        Assert.All(plan.Options, H.AssertSound);
        Assert.DoesNotContain(plan.Options, o => H.Uses(o, "09:30", TransportMode.DomesticFlight));

        var fastest = plan.Options.Single(o => o.Labels.Contains(TripLabel.Fastest));
        Assert.Equal("14:30", H.Hhmm(fastest.ArriveAt));
        // Ferry or speedboat both arrive in time; the shape of the journey is the same.
        Assert.Equal(
            [TransportMode.Walk, TransportMode.Ferry, TransportMode.Taxi, TransportMode.DomesticFlight, TransportMode.Walk],
            fastest.Legs.Select(l => l.Mode == TransportMode.Speedboat ? TransportMode.Ferry : l.Mode).ToArray());
        Assert.Equal("13:00", H.Hhmm(fastest.Legs.Single(l => l.Mode == TransportMode.DomesticFlight).DepartAt));
    }

    [Fact]
    public async Task Required_buffers_match_the_hand_computed_numbers()
    {
        var planner = H.Planner(W.World);
        var plan = await planner.PlanAsync(H.Q(W.Maafushi, W.Kooddoo, "2026-10-27", "05:00"), H.Ct);
        var option = plan.Options.First(o => o.Legs.Any(l => l.Mode == TransportMode.Ferry) && H.Uses(o, "13:00", TransportMode.DomesticFlight));

        Assert.Equal(18, option.Buffers[0].RequiredMinutes); // walk -> ferry: 3 + 15
        Assert.Equal(10, option.Buffers[1].RequiredMinutes); // ferry -> taxi: max(10, 8)
        Assert.Equal(48, option.Buffers[2].RequiredMinutes); // taxi -> flight: 3 + 45
        Assert.Equal(20, option.Buffers[3].RequiredMinutes); // flight -> walk: 20 + 0
    }

    [Fact]
    public async Task Option_ids_leg_ids_and_order_are_deterministic()
    {
        var a = await H.Planner(W.World).PlanAsync(H.Q(W.Maafushi, W.Kooddoo, "2026-10-27", "05:00"), H.Ct);
        var b = await H.Planner(W.World).PlanAsync(H.Q(W.Maafushi, W.Kooddoo, "2026-10-27", "05:00"), H.Ct);
        Assert.Equal(a.Result.SearchId, b.Result.SearchId);
        Assert.Equal(a.Options.Select(o => o.Id), b.Options.Select(o => o.Id));
        Assert.Equal(a.Options.SelectMany(o => o.Legs).Select(l => l.Id), b.Options.SelectMany(o => o.Legs).Select(l => l.Id));
        Assert.Equal(a.Options.Select(o => o.Score), b.Options.Select(o => o.Score));
    }

    [Fact]
    public async Task Arrive_by_never_arrives_late_and_leaves_as_late_as_safely_possible()
    {
        var plan = await H.Planner(W.World).PlanAsync(
            H.Q(W.Maafushi, W.Kooddoo, "2026-10-27", "17:00", TimeMode.ArriveBy), H.Ct);

        Assert.NotEmpty(plan.Options);
        Assert.All(plan.Options, H.AssertSound);
        var deadline = H.At("2026-10-27 17:00");
        Assert.All(plan.Options, o => Assert.True(o.ArriveAt <= deadline));
        // The 16:00 flight lands 17:05 (late) and the 14:00 ferry cannot reach the 16:00 flight: 13:00 is the last usable flight.
        Assert.DoesNotContain(plan.Options, o => H.Uses(o, "16:00", TransportMode.DomesticFlight));
        var latest = plan.Options.Single(o => o.Labels.Contains(TripLabel.Fastest));
        Assert.All(plan.Options, o => Assert.True(o.DepartAt <= latest.DepartAt));
    }

    [Fact]
    public async Task Cheapest_and_fastest_labels_are_consistent_with_the_numbers()
    {
        var plan = await H.Planner(W.World).PlanAsync(H.Q(W.Maafushi, W.Kooddoo, "2026-10-27", "05:00"), H.Ct);
        var cheapest = plan.Options.Single(o => o.Labels.Contains(TripLabel.Cheapest));
        var fastest = plan.Options.Single(o => o.Labels.Contains(TripLabel.Fastest));
        Assert.All(plan.Options, o => Assert.True(o.Price.Total.AmountMinor >= cheapest.Price.Total.AmountMinor));
        Assert.All(plan.Options, o => Assert.True(o.ArriveAt >= fastest.ArriveAt));
        Assert.Contains(plan.Options, o => o.Labels.Contains(TripLabel.Safest));
        Assert.Contains(plan.Options, o => o.Labels.Contains(TripLabel.BestOverall));
        // Cheapest: ferry (MVR 90) + taxi (80) + flight (1850), walks free.
        Assert.Equal(90_00 + 80_00 + 1850_00, cheapest.Price.Total.AmountMinor);
    }

    [Fact]
    public async Task Prices_scale_with_the_party_and_taxis_are_per_vehicle()
    {
        var one = await H.Planner(W.World).PlanAsync(H.Q(W.Maafushi, W.Kooddoo, "2026-10-27", "05:00", adults: 1), H.Ct);
        var five = await H.Planner(W.World).PlanAsync(H.Q(W.Maafushi, W.Kooddoo, "2026-10-27", "05:00", adults: 5), H.Ct);
        var c1 = one.Options.Single(o => o.Labels.Contains(TripLabel.Cheapest));
        var c5 = five.Options.Single(o => o.Labels.Contains(TripLabel.Cheapest));
        // 5 adults: ferry and flight x5, taxi needs 2 vehicles (capacity 4).
        Assert.Equal(5 * (90_00 + 1850_00) + 2 * 80_00, c5.Price.Total.AmountMinor);
        Assert.True(c5.Price.Total.AmountMinor > c1.Price.Total.AmountMinor);
    }

    [Fact]
    public async Task Larger_parties_need_longer_buffers()
    {
        var plan = await H.Planner(W.World).PlanAsync(H.Q(W.Maafushi, W.Kooddoo, "2026-10-27", "05:00", adults: 6), H.Ct);
        var o = plan.Options.First(x => H.Uses(x, "13:00", TransportMode.DomesticFlight));
        // 6 passengers: (6 - 2) * 2 = 8 extra minutes on every connection.
        Assert.Equal(10 + 8, o.Buffers[1].RequiredMinutes);
        Assert.Equal(48 + 8, o.Buffers[2].RequiredMinutes);
    }

    [Fact]
    public async Task Last_service_of_the_day_is_flagged_on_the_leg_and_in_the_explanations()
    {
        var plan = await H.Planner(W.World).PlanAsync(H.Q(W.Maafushi, W.Terminal, "2026-10-27", "15:00"), H.Ct);
        var option = plan.Options.Single(o => o.Legs.Any(l => l.Mode == TransportMode.Ferry));
        var ferry = option.Legs.Single(l => l.Mode == TransportMode.Ferry);
        Assert.Equal("16:30", H.Hhmm(ferry.DepartAt));
        Assert.Contains("LAST_SERVICE_OF_DAY", ferry.NoteCodes);
        var why = option.Explanations.Single(e => e.Code == "LAST_SERVICE_OF_DAY");
        Assert.Equal("ferry", why.Params["mode"]);
        Assert.Equal(Severity.Caution, why.Severity);
    }

    [Fact]
    public async Task Estimated_taxi_fares_are_flagged_everywhere()
    {
        var plan = await H.Planner(W.World).PlanAsync(H.Q(W.Maafushi, W.Kooddoo, "2026-10-27", "05:00"), H.Ct);
        var o = plan.Options.First(x => x.Legs.Any(l => l.Mode == TransportMode.Taxi));
        var taxi = o.Legs.Single(l => l.Mode == TransportMode.Taxi);
        Assert.Equal(Dhathuru.Domain.Transport.PricingKind.Estimate, taxi.Pricing);
        Assert.Contains("ESTIMATED_TAXI_FARE", taxi.NoteCodes);
        Assert.Contains(o.Warnings, w => w.Code == "ESTIMATED_TAXI_FARE");
        Assert.Contains(o.Explanations, e => e.Code == "ESTIMATED_TAXI_FARE");
    }

    [Fact]
    public async Task Assumed_walks_between_a_jetty_and_its_island_are_flagged()
    {
        var plan = await H.Planner(W.World).PlanAsync(H.Q(W.Maafushi, W.Kooddoo, "2026-10-27", "05:00"), H.Ct);
        var o = plan.Options[0];
        Assert.Contains(o.Warnings, w => w.Code == "ASSUMED_LINK");
        Assert.Contains("ASSUMED_LINK", o.Legs[0].NoteCodes);
    }

    [Fact]
    public async Task Option_can_be_read_back_by_id()
    {
        var planner = H.Planner(W.World);
        var plan = await planner.PlanAsync(H.Q(W.Maafushi, W.Kooddoo, "2026-10-27", "05:00"), H.Ct);
        var first = plan.Options[0];
        Assert.Same(first, await planner.GetOptionAsync(first.Id, H.Ct));
        Assert.Null(await planner.GetOptionAsync(Dhathuru.Domain.Ids.TripOptionId.New(), H.Ct));
    }

    [Fact]
    public async Task Mode_budget_and_transfer_filters_are_honoured()
    {
        var planner = H.Planner(W.World);
        var noBoats = await planner.PlanAsync(
            H.Q(W.Maafushi, W.Terminal, "2026-10-27", "05:00", modes: [TransportMode.Speedboat]), H.Ct);
        Assert.NotEmpty(noBoats.Options);
        Assert.All(noBoats.Options, o => Assert.DoesNotContain(o.Legs, l => l.Mode == TransportMode.Ferry));

        var cheapCap = await planner.PlanAsync(
            H.Q(W.Maafushi, W.Terminal, "2026-10-27", "05:00", budget: Dhathuru.Domain.Pricing.Money.FromMvr(100)), H.Ct);
        Assert.NotEmpty(cheapCap.Options);
        Assert.All(cheapCap.Options, o => Assert.True(o.Price.Total.AmountMinor <= 100_00));

        var capped = await planner.PlanAsync(H.Q(W.Maafushi, W.Kooddoo, "2026-10-27", "05:00", maxTransfers: 2), H.Ct);
        Assert.All(capped.Options, o => Assert.True(o.Transfers <= 2));
    }

    [Fact]
    public async Task Never_returns_more_than_six_and_always_labels_the_four_winners()
    {
        var plan = await H.Planner(W.World).PlanAsync(H.Q(W.Maafushi, W.Kooddoo, "2026-10-27", "05:00"), H.Ct);
        Assert.InRange(plan.Options.Count, 1, 6);
        var labels = plan.Options.SelectMany(o => o.Labels).ToHashSet();
        Assert.Equal(4, labels.Count);
    }
}
