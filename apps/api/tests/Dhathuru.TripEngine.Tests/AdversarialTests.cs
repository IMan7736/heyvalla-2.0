using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Places;
using Dhathuru.Domain.Transport;
using Dhathuru.Domain.Trips;
using Dhathuru.TripEngine.Fixtures;

namespace Dhathuru.TripEngine.Tests;

/// <summary>The cases that make a naive planner lie: one minute short, closed taxis, cancelled legs, nothing to offer.</summary>
public class AdversarialTests
{
    /// <summary>Ferry lands 09:00; taxi 09:10-09:25; a flight needs 48 min after the taxi, so 10:13 is the first reachable.</summary>
    private static (WorldBuilder W, PlaceId From, PlaceId To) FerryTaxiFlight(string flightDep, int cutoff = 45)
    {
        var w = new WorldBuilder();
        var island = w.Place("A", PlaceType.Island);
        var jetty = w.Place("A Jetty", PlaceType.Jetty, island);
        var terminal = w.Place("Terminal", PlaceType.Jetty);
        var mle = w.Place("MLE", PlaceType.Airport);
        var gkk = w.Place("GKK", PlaceType.Airport);
        w.Link(terminal, mle, TransportMode.Taxi, 15, 80);
        w.Run("ferry", TransportMode.Ferry, jetty, terminal, "2026-10-27 07:30", 90, 90);
        w.Run("air", TransportMode.DomesticFlight, mle, gkk, $"2026-10-27 {flightDep}", 60, 1500, cutoff);
        return (w, island, gkk);
    }

    [Fact]
    public async Task A_flight_exactly_at_the_minimum_buffer_is_returned_but_flagged_high_risk()
    {
        var (w, from, to) = FerryTaxiFlight("10:13");
        var plan = await H.Planner(w).PlanAsync(H.Q(from, to, "2026-10-27", "05:00"), H.Ct);

        var option = Assert.Single(plan.Options);
        H.AssertSound(option);
        Assert.Equal(0, option.Buffers[^1].Minutes - option.Buffers[^1].RequiredMinutes);
        Assert.Equal(RiskLevel.High, option.Risk.Level);
        Assert.Contains(option.Warnings, x => x.Code == "RISK_ABOVE_TOLERANCE");
        Assert.Contains(plan.Notices, n => n.Code == "NO_OPTION_WITHIN_RISK_TOLERANCE");
    }

    [Fact]
    public async Task Last_boat_missed_by_one_minute_is_never_offered()
    {
        var (w, from, to) = FerryTaxiFlight("10:12");
        var plan = await H.Planner(w).PlanAsync(H.Q(from, to, "2026-10-27", "05:00"), H.Ct);

        Assert.Empty(plan.Options);
        Assert.Contains(plan.Notices, n => n.Code == "CONNECTION_TOO_TIGHT");
        Assert.Contains(plan.Notices, n => n.Code == "NO_SAFE_CONNECTION");
    }

    [Fact]
    public async Task A_longer_check_in_cutoff_moves_the_first_reachable_flight()
    {
        // 90 min cutoff: the taxi must land by 08:55-ish, impossible; a 10:55 flight is the first that works.
        var (w, from, to) = FerryTaxiFlight("10:13", cutoff: 90);
        var plan = await H.Planner(w).PlanAsync(H.Q(from, to, "2026-10-27", "05:00"), H.Ct);
        Assert.Empty(plan.Options);

        var (w2, from2, to2) = FerryTaxiFlight("10:58", cutoff: 90);
        var plan2 = await H.Planner(w2).PlanAsync(H.Q(from2, to2, "2026-10-27", "05:00"), H.Ct);
        Assert.NotEmpty(plan2.Options);
    }

    [Fact]
    public async Task The_arrive_by_deadline_is_never_missed_by_a_minute()
    {
        var (w, from, to) = FerryTaxiFlight("10:13");
        // Flight lands 11:13. A deadline of 11:12 must give nothing; 11:13 is allowed (flagged high risk).
        var late = await H.Planner(w).PlanAsync(H.Q(from, to, "2026-10-27", "11:12", TimeMode.ArriveBy), H.Ct);
        Assert.Empty(late.Options);
        Assert.Contains(late.Notices, n => n.Code == "EARLIEST_POSSIBLE_ARRIVAL");

        var ok = await H.Planner(w).PlanAsync(H.Q(from, to, "2026-10-27", "11:13", TimeMode.ArriveBy), H.Ct);
        Assert.NotEmpty(ok.Options);
        Assert.All(ok.Options, o => Assert.True(o.ArriveAt <= H.At("2026-10-27 11:13")));
    }

    [Fact]
    public async Task Taxis_do_not_run_at_night_and_the_trip_waits_for_the_morning_instead()
    {
        // Late ferry lands 23:45 (dep 22:15). Taxi window is 05:00-23:30. The 06:30 flight is reachable only after an overnight stay.
        var w = new WorldBuilder();
        var island = w.Place("A", PlaceType.Island);
        var jetty = w.Place("A Jetty", PlaceType.Jetty, island);
        var terminal = w.Place("Terminal", PlaceType.Jetty);
        var mle = w.Place("MLE", PlaceType.Airport);
        var gkk = w.Place("GKK", PlaceType.Airport);
        w.Link(terminal, mle, TransportMode.Taxi, 15, 80);
        w.Run("ferry", TransportMode.Ferry, jetty, terminal, "2026-10-27 22:15", 90, 90);
        w.Run("air", TransportMode.DomesticFlight, mle, gkk, "2026-10-28 06:30", 60, 1500, 45);

        var plan = await H.Planner(w).PlanAsync(H.Q(island, gkk, "2026-10-27", "20:00"), H.Ct);

        var option = Assert.Single(plan.Options);
        H.AssertSound(option);
        var taxi = option.Legs.Single(l => l.Mode == TransportMode.Taxi);
        var local = Dhathuru.Domain.Time.MaldivesTime.ToLocal(taxi.DepartAt);
        Assert.InRange(local.Hour * 60 + local.Minute, 5 * 60, 23 * 60 + 30);
        Assert.Contains(option.Explanations, e => e.Code == "OVERNIGHT_REQUIRED");
    }

    [Fact]
    public async Task When_the_taxi_is_the_only_link_and_it_is_closed_the_answer_says_so()
    {
        var w = new WorldBuilder();
        var island = w.Place("A", PlaceType.Island);
        var jetty = w.Place("A Jetty", PlaceType.Jetty, island);
        var terminal = w.Place("Terminal", PlaceType.Jetty);
        var mle = w.Place("MLE", PlaceType.Airport);
        var gkk = w.Place("GKK", PlaceType.Airport);
        w.Link(terminal, mle, TransportMode.Taxi, 15, 80);
        w.Run("ferry", TransportMode.Ferry, jetty, terminal, "2026-10-27 22:15", 90, 90);
        w.Run("air", TransportMode.DomesticFlight, mle, gkk, "2026-10-28 00:30", 60, 1500, 45);

        var plan = await H.Planner(w).PlanAsync(H.Q(island, gkk, "2026-10-27", "20:00"), H.Ct);

        Assert.Empty(plan.Options);
        Assert.Contains(plan.Notices, n => n.Code == "TAXI_UNAVAILABLE_AT_NIGHT");
        Assert.Contains(plan.Notices, n => n.Code == "NO_SAFE_CONNECTION");
    }

    [Fact]
    public async Task No_route_unknown_place_and_same_place_return_explanatory_codes()
    {
        var m = new MiniMaldives();
        var planner = H.Planner(m.World);

        var none = await planner.PlanAsync(H.Q(m.Maafushi, m.Island, "2026-10-27", "08:00"), H.Ct);
        Assert.Empty(none.Options);
        Assert.Contains(none.Notices, n => n.Code == "NO_ROUTE_EXISTS");
        Assert.Contains(none.Notices, n => n.Code == "NO_SERVICES_AT_DESTINATION");

        var ghost = Dhathuru.Domain.Ids.PlaceId.Parse("isl_01K8Z3M5Q7R9T2V4W6X8Y0A1B2");
        var unknown = await planner.PlanAsync(H.Q(ghost, m.Kooddoo, "2026-10-27", "08:00"), H.Ct);
        Assert.Contains(unknown.Notices, n => n.Code == "PLACE_UNKNOWN" && (string?)n.Params["role"] == "origin");

        var same = await planner.PlanAsync(H.Q(m.Kooddoo, m.Kooddoo, "2026-10-27", "08:00"), H.Ct);
        Assert.Contains(same.Notices, n => n.Code == "ORIGIN_EQUALS_DESTINATION");
    }

    [Fact]
    public async Task Constraints_that_cause_an_empty_result_are_named_with_a_suggestion()
    {
        var m = new MiniMaldives();
        var planner = H.Planner(m.World);

        var modes = await planner.PlanAsync(H.Q(m.Maafushi, m.Terminal, "2026-10-27", "08:00", modes: [TransportMode.Bus]), H.Ct);
        Assert.Empty(modes.Options);
        Assert.Contains(modes.Notices, n => n.Code == "MODE_FILTER_EXCLUDES_ALL");

        var budget = await planner.PlanAsync(H.Q(m.Maafushi, m.Terminal, "2026-10-27", "08:00", budget: Dhathuru.Domain.Pricing.Money.FromMvr(10)), H.Ct);
        Assert.Empty(budget.Options);
        Assert.Contains(budget.Notices, n => n.Code == "BUDGET_EXCEEDED");
        Assert.Contains(budget.Notices, n => n.Code == "SUGGEST_INCREASE_BUDGET");

        var transfers = await planner.PlanAsync(H.Q(m.Maafushi, m.Kooddoo, "2026-10-27", "05:00", maxTransfers: 1), H.Ct);
        Assert.Empty(transfers.Options);
        Assert.Contains(transfers.Notices, n => n.Code == "MAX_TRANSFERS_EXCEEDED");
    }

    [Fact]
    public async Task Options_that_already_left_are_never_returned_in_either_direction()
    {
        // H.Now is 2026-10-26 12:00 local. The 08:00 boat on the 26th has gone; the 27th is fine.
        var w = new WorldBuilder();
        var a = w.Place("A", PlaceType.Jetty);
        var b = w.Place("B", PlaceType.Jetty);
        w.Run("early", TransportMode.Ferry, a, b, "2026-10-26 08:00", 60, 100);
        w.Run("next", TransportMode.Ferry, a, b, "2026-10-27 08:00", 60, 100);
        var planner = H.Planner(w);

        var back = await planner.PlanAsync(H.Q(a, b, "2026-10-27", "20:00", mode: TimeMode.ArriveBy), H.Ct);
        Assert.NotEmpty(back.Options);
        Assert.All(back.Options, o => Assert.True(o.DepartAt >= H.Now));

        var fwd = await planner.PlanAsync(H.Q(a, b, "2026-10-26", "06:00"), H.Ct);
        Assert.NotEmpty(fwd.Options);
        Assert.All(fwd.Options, o => Assert.True(o.DepartAt >= H.Now));
    }

    private static PlaceId Id(WorldBuilder w, string name) => w.Names.First(p => p.Value == name).Key;

    [Fact]
    public async Task Empty_results_carry_checked_suggestions()
    {
        var m = new MiniMaldives();
        var planner = H.Planner(m.World);

        var modes = await planner.PlanAsync(H.Q(m.Maafushi, m.Terminal, "2026-10-27", "08:00", modes: [TransportMode.Bus]), H.Ct);
        Assert.Contains(modes.SuggestionList, s => s.Code == "ALLOW_ALL_MODES");

        var budget = await planner.PlanAsync(H.Q(m.Maafushi, m.Terminal, "2026-10-27", "08:00", budget: Dhathuru.Domain.Pricing.Money.FromMvr(10)), H.Ct);
        Assert.Contains(budget.SuggestionList, s => s.Code == "REMOVE_BUDGET");

        var transfers = await planner.PlanAsync(H.Q(m.Maafushi, m.Kooddoo, "2026-10-27", "05:00", maxTransfers: 1), H.Ct);
        Assert.Contains(transfers.SuggestionList, s => s.Code == "ALLOW_MORE_TRANSFERS");

        // Nothing runs the next day: honest silence, no invented suggestion.
        var (w, from, to) = FerryTaxiFlight("10:12");
        var none = await H.Planner(w).PlanAsync(H.Q(from, to, "2026-10-27", "05:00"), H.Ct);
        Assert.Empty(none.Options);
        Assert.Empty(none.SuggestionList);

        // With a working day after, tomorrow is suggested.
        var (w2, from2, to2) = FerryTaxiFlight("10:12");
        w2.Run("ferry2", TransportMode.Ferry, Id(w2, "A Jetty"), Id(w2, "Terminal"), "2026-10-28 07:30", 90, 90);
        w2.Run("air2", TransportMode.DomesticFlight, Id(w2, "MLE"), Id(w2, "GKK"), "2026-10-28 11:00", 60, 1500, 45);
        var late = await H.Planner(w2).PlanAsync(H.Q(from2, to2, "2026-10-27", "05:00"), H.Ct);
        Assert.Empty(late.Options);
        Assert.Contains(late.SuggestionList, s => s.Code == "TRY_NEXT_DAY");

        var ok = await planner.PlanAsync(H.Q(m.Maafushi, m.Terminal, "2026-10-27", "08:00"), H.Ct);
        Assert.Empty(ok.SuggestionList);
    }

    [Fact]
    public async Task A_deadline_in_the_past_is_refused_with_a_code()
    {
        var m = new MiniMaldives();
        var plan = await H.Planner(m.World).PlanAsync(H.Q(m.Maafushi, m.Terminal, "2026-10-26", "09:00", TimeMode.ArriveBy), H.Ct);
        Assert.Empty(plan.Options);
        Assert.Contains(plan.Notices, n => n.Code == "DEADLINE_IN_PAST");
    }

    [Fact]
    public async Task Synthetic_timetables_are_flagged_so_the_ui_can_say_so()
    {
        var w = new WorldBuilder();
        var a = w.Place("A", PlaceType.Jetty);
        var b = w.Place("B", PlaceType.Jetty);
        w.Run("ferry", TransportMode.Ferry, a, b, "2026-10-27 09:00", 60, 50, synthetic: true);
        var plan = await H.Planner(w).PlanAsync(H.Q(a, b, "2026-10-27", "08:00"), H.Ct);
        var option = Assert.Single(plan.Options);
        Assert.Contains(option.Warnings, x => x.Code == "SYNTHETIC_TIMETABLE");
        Assert.Contains("SYNTHETIC_TIMETABLE", option.Legs[0].NoteCodes);
    }

    [Fact]
    public async Task Poor_delay_history_lowers_trust_and_is_explained()
    {
        var w = new WorldBuilder();
        var a = w.Place("A", PlaceType.Jetty);
        var b = w.Place("B", PlaceType.Jetty);
        var c = w.Place("C", PlaceType.Jetty);
        w.Run("flaky", TransportMode.Ferry, a, b, "2026-10-27 09:00", 60, 50);
        w.Run("next", TransportMode.Ferry, b, c, "2026-10-27 10:40", 60, 50);
        var stats = new Dictionary<Dhathuru.Domain.Ids.ServiceId, Dhathuru.Domain.Seams.ReliabilityStat>
        {
            [w.Service("flaky")] = new(w.Service("flaky"), 0.55, 45, 400),
        };
        var plan = await H.Planner(w, stats: stats).PlanAsync(H.Q(a, c, "2026-10-27", "08:00", tolerance: RiskTolerance.Relaxed), H.Ct);
        var option = Assert.Single(plan.Options);
        var poor = option.Explanations.Single(e => e.Code == "DELAY_HISTORY_POOR");
        Assert.Equal(55, poor.Params["percent"]);

        var clean = await H.Planner(w).PlanAsync(H.Q(a, c, "2026-10-27", "08:00", tolerance: RiskTolerance.Relaxed), H.Ct);
        Assert.True(option.Risk.MissProbability > clean.Options[0].Risk.MissProbability);
    }

    [Fact]
    public async Task Reliability_failures_degrade_to_priors_with_a_notice_never_to_perfection()
    {
        var m = new MiniMaldives();
        var planner = new TripPlanner(
            new StaticSnapshotProvider(m.World.Build()), new ThrowingReliability(), null, null, null, new FixedClock(H.Now));
        var plan = await planner.PlanAsync(H.Q(m.Maafushi, m.Kooddoo, "2026-10-27", "05:00"), H.Ct);
        Assert.NotEmpty(plan.Options);
        Assert.Contains(plan.Notices, n => n.Code == "RELIABILITY_UNAVAILABLE");
    }

    private sealed class ThrowingReliability : Dhathuru.Domain.Seams.IReliabilityStats
    {
        public Task<Dhathuru.Domain.Seams.ReliabilityStat?> GetAsync(Dhathuru.Domain.Ids.ServiceId serviceId, CancellationToken ct) =>
            throw new InvalidOperationException("stats store is down");
    }
}
