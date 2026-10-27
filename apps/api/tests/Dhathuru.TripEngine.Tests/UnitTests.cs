using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Places;
using Dhathuru.Domain.Seams;
using Dhathuru.Domain.Transport;
using Dhathuru.Domain.Trips;
using Dhathuru.TripEngine.Configuration;
using Dhathuru.TripEngine.Delays;
using Dhathuru.TripEngine.Fixtures;
using Dhathuru.TripEngine.Network;
using Dhathuru.TripEngine.Support;

namespace Dhathuru.TripEngine.Tests;

public class DelayMathTests
{
    [Fact]
    public void Exceedance_decreases_with_slack_and_is_certain_when_negative()
    {
        var d = new DelayDist(0.3, 12);
        Assert.Equal(1, d.Exceeds(-1));
        Assert.Equal(0.3, d.Exceeds(0), 9);
        Assert.True(d.Exceeds(10) > d.Exceeds(20));
        Assert.Equal(0, DelayDist.None.Exceeds(0));
    }

    [Fact]
    public void Sum_of_two_delays_matches_a_monte_carlo_estimate()
    {
        var a = new DelayDist(0.3, 12);
        var b = new DelayDist(0.15, 6);
        var rng = new Random(42);
        const int n = 400_000;
        foreach (var x in new[] { 0.0, 5, 15, 30 })
        {
            var hits = 0;
            for (var i = 0; i < n; i++)
            {
                var da = rng.NextDouble() < a.LateProbability ? -a.MeanLateMin * Math.Log(1 - rng.NextDouble()) : 0;
                var db = rng.NextDouble() < b.LateProbability ? -b.MeanLateMin * Math.Log(1 - rng.NextDouble()) : 0;
                if (da + db > x) hits++;
            }
            Assert.Equal(hits / (double)n, DelayDist.SumExceeds(a, b, x), 2);
        }
    }

    [Fact]
    public void Equal_means_use_the_erlang_form_without_dividing_by_zero()
    {
        var a = new DelayDist(1, 10);
        var p = DelayDist.SumExceeds(a, a, 10);
        Assert.Equal(Math.Exp(-1) * 2, p, 9);
    }

    [Fact]
    public void Quantile_inverts_exceedance()
    {
        var d = new DelayDist(0.4, 9);
        Assert.Equal(0.1, d.Exceeds(d.Quantile(0.1)), 9);
        Assert.Equal(0, new DelayDist(0.05, 9).Quantile(0.1));
    }

    [Fact]
    public void Merge_is_never_less_late_than_either_part()
    {
        var m = DelayDist.Merge(new DelayDist(0.2, 5), new DelayDist(0.1, 20));
        Assert.True(m.LateProbability >= 0.2);
        Assert.InRange(m.MeanLateMin, 5, 20);
    }

    [Fact]
    public void No_history_means_the_prior_not_perfection_and_history_is_blended_by_sample_count()
    {
        var opts = new DelayPriorOptions();
        var svc = ServiceId.New();
        var none = new DelayModel(opts).ForRun(TransportMode.Ferry, svc.Value);
        Assert.Equal(opts.Ferry.LateProbability, none.LateProbability);

        DelayDist With(int samples) => new DelayModel(opts, new Dictionary<string, ReliabilityStat?>
        {
            [svc.Value] = new(svc, 0.5, 40, samples),
        }).ForRun(TransportMode.Ferry, svc.Value);

        Assert.True(With(5).LateProbability < With(500).LateProbability);
        Assert.InRange(With(100000).LateProbability, 0.49, 0.51);
        Assert.True(With(500).LateProbability > none.LateProbability);
    }

    [Fact]
    public void Taxi_noise_grows_with_ride_length_and_walking_has_none()
    {
        var model = new DelayModel(new DelayPriorOptions());
        Assert.True(model.ForFlexible(TransportMode.Taxi, 60).MeanLateMin > model.ForFlexible(TransportMode.Taxi, 10).MeanLateMin);
        Assert.True(model.ForFlexible(TransportMode.Walk, 10).IsNone);
    }
}

public class RuleTests
{
    private static Hop Run(TransportMode mode, int cutoff = 0) => new()
    {
        Kind = HopKind.Scheduled, Mode = mode, From = PlaceId.Parse("jty_00000000000000000000000001"), To = PlaceId.Parse("jty_00000000000000000000000002"),
        Dep = 0, Arr = 30, Fare = Dhathuru.Domain.Pricing.Money.Zero, Pricing = PricingKind.Fixed, CheckInCutoffMin = cutoff,
    };

    [Theory]
    [InlineData(TransportMode.Ferry, TransportMode.DomesticFlight, 45, 10 + 45)]
    [InlineData(TransportMode.Ferry, TransportMode.DomesticFlight, 0, 10 + 45)]
    [InlineData(TransportMode.DomesticFlight, TransportMode.Ferry, 0, 20 + 15)]
    [InlineData(TransportMode.DomesticFlight, TransportMode.Seaplane, 0, 20 + 30)]
    [InlineData(TransportMode.Speedboat, TransportMode.Bus, 0, 10 + 5)]
    [InlineData(TransportMode.DomesticFlight, TransportMode.Taxi, 0, 20)]
    [InlineData(TransportMode.Ferry, TransportMode.Taxi, 0, 10)]
    [InlineData(TransportMode.Taxi, TransportMode.Taxi, 0, 8)]
    public void Required_buffer_by_mode_pair(TransportMode prev, TransportMode next, int cutoff, int expected)
    {
        var rules = new ConnectionRules(new EngineOptions(), 1);
        Assert.Equal(expected, rules.Required(Run(prev), Run(next, cutoff)));
    }

    [Fact]
    public void Starting_at_the_place_needs_only_the_boarding_rule_and_ending_needs_nothing()
    {
        var rules = new ConnectionRules(new EngineOptions(), 1);
        Assert.Equal(45, rules.Required(null, Run(TransportMode.DomesticFlight)));
        Assert.Equal(0, rules.Required(Run(TransportMode.DomesticFlight), null));
    }

    [Fact]
    public void Extra_passengers_and_hub_overrides_add_time()
    {
        var next = Run(TransportMode.Ferry);
        var opts = new EngineOptions { Connections = new ConnectionOptions { ExtraMinutesByPlace = new Dictionary<string, int> { [next.From.Value] = 7 } } };
        Assert.Equal(25 + 2 * 3 + 7, new ConnectionRules(opts, 5).Required(Run(TransportMode.Ferry), next) );
    }

    [Fact]
    public void Taxi_window_moves_a_night_ride_to_the_morning_and_back_to_the_evening()
    {
        var rules = new ConnectionRules(new EngineOptions(), 1);
        var midnight = TimeMath.Minute(Dhathuru.Domain.Time.MaldivesTime.FromLocal(new DateOnly(2026, 10, 28), new TimeOnly(0, 10)));
        var morning = rules.NextTaxiDeparture(midnight, 15);
        Assert.Equal(5 * 60, TimeMath.LocalMinuteOfDay(morning!.Value));
        var back = rules.PrevTaxiArrival(midnight, 15);
        Assert.Equal(23 * 60 + 30, TimeMath.LocalMinuteOfDay(back!.Value));
        Assert.Null(rules.NextTaxiDeparture(midnight, 20 * 60));
    }
}

public class IdAndStoreTests
{
    [Fact]
    public void Stable_ids_are_valid_ulids_and_deterministic()
    {
        var a = StableIds.Leg("x");
        Assert.True(LegId.TryParse(a.Value, out _));
        Assert.Equal(a, StableIds.Leg("x"));
        Assert.NotEqual(a, StableIds.Leg("y"));
        Assert.True(TripOptionId.TryParse(StableIds.Option("x").Value, out _));
        Assert.True(SearchId.TryParse(StableIds.Search("x").Value, out _));
        for (var i = 0; i < 500; i++) Assert.True(LegId.TryParse(StableIds.Leg("n" + i).Value, out _));
    }

    [Fact]
    public async Task Option_store_evicts_the_oldest_beyond_capacity_and_expires_entries()
    {
        var m = new MiniMaldives();
        var plan = await H.Planner(m.World).PlanAsync(H.Q(m.Maafushi, m.Kooddoo, "2026-10-27", "05:00"), H.Ct);
        var clock = new MutableClock(H.Now);
        var store = new InMemoryOptionStore(clock, capacity: 2, timeToLive: TimeSpan.FromMinutes(10));
        var options = plan.Options.Take(3).ToList();
        Assert.True(options.Count >= 2, "need at least two options to test eviction");
        var small = new InMemoryOptionStore(clock, capacity: 1, timeToLive: TimeSpan.FromMinutes(10));
        small.Put(options[0]);
        small.Put(options[1]);
        Assert.Null(small.Get(options[0].Id));
        Assert.NotNull(small.Get(options[1].Id));
        clock.Now = clock.Now.AddMinutes(11);
        Assert.Null(small.Get(options[1].Id));
    }

    private sealed class MutableClock(DateTimeOffset start) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = start;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}

public class DiversityTests
{
    [Fact]
    public async Task Ten_flights_ten_minutes_apart_do_not_fill_the_list_with_near_duplicates()
    {
        var w = new WorldBuilder();
        var a = w.Place("A", PlaceType.Airport);
        var b = w.Place("B", PlaceType.Airport);
        for (var i = 0; i < 10; i++)
            w.Run("air", TransportMode.DomesticFlight, a, b, $"2026-10-27 {10 + i / 6:00}:{i % 6 * 10:00}", 60, 1000, 45);
        w.Run("slow", TransportMode.Ferry, a, b, "2026-10-27 07:00", 300, 100);

        var plan = await H.Planner(w).PlanAsync(H.Q(a, b, "2026-10-27", "06:00"), H.Ct);

        var flights = plan.Options.Where(o => o.Legs[0].Mode == TransportMode.DomesticFlight).ToList();
        Assert.True(flights.Count <= 4, "labelled winners at most");
        // Every flight offered only as a backup (no label) must sit at least 30 minutes from every other flight shown.
        foreach (var backup in flights.Where(o => o.Labels.Count == 0))
            Assert.All(flights.Where(o => o != backup), o => Assert.True(Math.Abs((o.DepartAt - backup.DepartAt).TotalMinutes) >= 30));
        Assert.Contains(plan.Options, o => o.Legs[0].Mode == TransportMode.Ferry); // the cheap, slow alternative survives
    }

    [Fact]
    public async Task A_slower_trip_with_far_lower_miss_risk_is_preferred_by_a_cautious_traveller()
    {
        var w = new WorldBuilder();
        var a = w.Place("A", PlaceType.Jetty);
        var b = w.Place("B", PlaceType.Jetty);
        var c = w.Place("C", PlaceType.Jetty);
        w.Run("in", TransportMode.Ferry, a, b, "2026-10-27 08:00", 60, 50);
        w.Run("out", TransportMode.Ferry, b, c, "2026-10-27 09:35", 60, 50); // 35 min buffer after 25 required (10 alight + 15 boarding): 10 min slack, miss risk about 11 %
        w.Run("out", TransportMode.Ferry, b, c, "2026-10-27 11:30", 60, 50); // 150 min buffer: safe, 2 h later

        var cautious = await H.Planner(w).PlanAsync(H.Q(a, c, "2026-10-27", "07:00", tolerance: RiskTolerance.Cautious), H.Ct);
        var best = cautious.Options.Single(o => o.Labels.Contains(TripLabel.BestOverall));
        var fragile = cautious.Options.SingleOrDefault(o => H.Uses(o, "09:35", TransportMode.Ferry));
        Assert.Equal("11:30", H.Hhmm(best.Legs[^1].DepartAt));
        Assert.Null(fragile); // outside a cautious tolerance, hidden because a safe option exists

        var relaxed = await H.Planner(w).PlanAsync(H.Q(a, c, "2026-10-27", "07:00", tolerance: RiskTolerance.Relaxed), H.Ct);
        Assert.Contains(relaxed.Options, o => H.Uses(o, "09:35", TransportMode.Ferry));
        Assert.Equal("10:35", H.Hhmm(relaxed.Options.Single(o => o.Labels.Contains(TripLabel.Fastest)).ArriveAt));
    }
}
