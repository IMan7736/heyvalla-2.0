using System.Text.Json;
using Dhathuru.Domain.Seams;
using Dhathuru.Domain.Time;
using Dhathuru.Domain.Transport;
using Dhathuru.Domain.Trips;
using Dhathuru.TripEngine.Configuration;
using Dhathuru.TripEngine.Fixtures;
using FsCheck;
using FsCheck.Xunit;

namespace Dhathuru.TripEngine.Tests;

/// <summary>
/// Properties over random small networks (seeded, so any failure reproduces from its seed).
/// (a) no option violates a required buffer, (b) times are monotonic, (c) arrive-by never arrives late,
/// (d) removing a service never improves the best result, (e) results are deterministic.
/// </summary>
public class PropertyTests
{
    private static readonly TimeOnly[] Times = [new(6, 0), new(9, 30), new(12, 0), new(15, 45), new(19, 0)];

    private static (TripSearchQuery Query, RandomWorld World) Pick(int seed, TimeMode? forceMode = null, RiskTolerance? tol = null)
    {
        var world = new RandomWorld(seed);
        var r = new Random(unchecked(seed * 31 + 7));
        var from = world.Endpoints[r.Next(world.Endpoints.Count)];
        var to = world.Endpoints[r.Next(world.Endpoints.Count)];
        while (to == from) to = world.Endpoints[r.Next(world.Endpoints.Count)];
        var mode = forceMode ?? (r.Next(2) == 0 ? TimeMode.DepartAfter : TimeMode.ArriveBy);
        var time = Times[r.Next(Times.Length)];
        var tolerance = tol ?? (RiskTolerance)r.Next(3);
        var q = new TripSearchQuery(from, to, new DateOnly(2026, 10, 27), time, mode, new Passengers(r.Next(1, 5), r.Next(0, 2)), new TripPreferences(tolerance));
        return (q, world);
    }

    /// <summary>Tolerance filter off: for properties about reachability and speed, not about which risky trips get hidden.</summary>
    private static readonly EngineOptions Permissive = new()
    {
        Scoring = new ScoringOptions
        {
            Cautious = Profile(),
            Balanced = Profile(),
            Relaxed = Profile(),
        },
    };

    private static ToleranceProfile Profile() => new()
    {
        MaxMissProbability = 1, MinSlackMin = 0, ComfortMarginMin = 10, WeightTime = 1, WeightPrice = 0, WeightRisk = 0, WeightTransfers = 0,
    };

    private static TripPlanner PlannerFor(RandomWorld world, EngineOptions? options = null, Func<TimetableSnapshot, TimetableSnapshot>? edit = null)
    {
        var snapshot = world.World.Build();
        if (edit is not null) snapshot = edit(snapshot);
        return new TripPlanner(new StaticSnapshotProvider(snapshot), new StaticReliability(), null, options, null, new FixedClock(H.Now));
    }

    [Property(MaxTest = 120, QuietOnSuccess = true)]
    public void Options_are_sound_and_never_break_a_required_buffer(int seed)
    {
        var (q, world) = Pick(seed);
        var snapshot = world.World.Build();
        var plan = PlannerFor(world).PlanAsync(q, TestContext.Current.CancellationToken).GetAwaiter().GetResult();
        var runs = snapshot.Runs.ToDictionary(r => r.ServiceRunId);
        var anchor = MaldivesTime.FromLocal(q.Date, q.Time);

        Assert.True(plan.Options.Count <= 6);
        foreach (var o in plan.Options)
        {
            H.AssertSound(o);
            Assert.Equal(q.From, o.Legs[0].From.Id);
            Assert.Equal(q.To, o.Legs[^1].To.Id);
            if (q.TimeMode == TimeMode.ArriveBy) Assert.True(o.ArriveAt <= anchor, "arrived after the deadline");
            else Assert.True(o.DepartAt >= anchor, "departed before the search time");

            for (var i = 1; i < o.Legs.Count; i++)
            {
                var next = o.Legs[i];
                var gap = (next.DepartAt - o.Legs[i - 1].ArriveAt).TotalMinutes;
                // Independent floors, straight from the published rules.
                if (next.Mode == TransportMode.DomesticFlight)
                    Assert.True(gap >= runs[next.ServiceRunId!].CheckInCutoffMin, $"flight check-in cutoff violated ({gap} min)");
                if (next.Mode is TransportMode.Ferry or TransportMode.Speedboat)
                    Assert.True(gap >= 15, $"boat boarding closure violated ({gap} min)");
                if (next.Mode == TransportMode.Taxi)
                    Assert.True(gap >= 8, $"taxi pick-up lead violated ({gap} min)");
            }
            foreach (var taxi in o.Legs.Where(l => l.Mode == TransportMode.Taxi))
            {
                var dep = MaldivesTime.ToLocal(taxi.DepartAt);
                var arr = MaldivesTime.ToLocal(taxi.ArriveAt);
                Assert.True(dep.Hour * 60 + dep.Minute >= 5 * 60 && arr.Hour * 60 + arr.Minute <= 23 * 60 + 30 && arr.Date == dep.Date, "taxi outside its service window");
            }
        }
    }

    [Property(MaxTest = 120, QuietOnSuccess = true)]
    public void Same_input_and_snapshot_give_identical_output(int seed)
    {
        var (q, world) = Pick(seed);
        var a = PlannerFor(world).PlanAsync(q, TestContext.Current.CancellationToken).GetAwaiter().GetResult();
        var b = PlannerFor(world).PlanAsync(q, TestContext.Current.CancellationToken).GetAwaiter().GetResult();
        Assert.Equal(JsonSerializer.Serialize(a), JsonSerializer.Serialize(b));
    }

    [Property(MaxTest = 120, QuietOnSuccess = true)]
    public void Removing_a_service_never_improves_the_best_result(int seed, byte which)
    {
        var (q, world) = Pick(seed, TimeMode.DepartAfter);
        var permissive = Permissive;
        var victim = world.World.Service(world.ServiceNames[which % world.ServiceNames.Length]);

        var full = PlannerFor(world, permissive).PlanAsync(q, TestContext.Current.CancellationToken).GetAwaiter().GetResult();
        var reduced = PlannerFor(world, permissive, s => s with { Runs = s.Runs.Where(r => r.ServiceId != victim).ToList() })
            .PlanAsync(q, TestContext.Current.CancellationToken).GetAwaiter().GetResult();

        if (reduced.Options.Count == 0) return;
        Assert.NotEmpty(full.Options);
        var bestFull = full.Options.Min(o => o.ArriveAt);
        var bestReduced = reduced.Options.Min(o => o.ArriveAt);
        Assert.True(bestFull <= bestReduced, $"removing a service improved arrival from {bestFull:o} to {bestReduced:o}");
    }

    [Property(MaxTest = 120, QuietOnSuccess = true)]
    public void A_forward_search_from_a_found_departure_arrives_no_later(int seed)
    {
        var (q, world) = Pick(seed, TimeMode.ArriveBy);
        var planner = PlannerFor(world, Permissive);
        var plan = planner.PlanAsync(q, TestContext.Current.CancellationToken).GetAwaiter().GetResult();
        foreach (var o in plan.Options.Take(2))
        {
            // Be at the origin 30 minutes before the arrive-by plan leaves: a forward search must do at least as well.
            var start = MaldivesTime.ToLocal(o.DepartAt).AddMinutes(-30);
            var forward = q with
            {
                Date = DateOnly.FromDateTime(start.Date),
                Time = TimeOnly.FromDateTime(start.DateTime),
                TimeMode = TimeMode.DepartAfter,
            };
            var fwd = planner.PlanAsync(forward, TestContext.Current.CancellationToken).GetAwaiter().GetResult();
            Assert.NotEmpty(fwd.Options);
            Assert.True(fwd.Options.Min(x => x.ArriveAt) <= o.ArriveAt, "forward search found nothing as early as the arrive-by option");
        }
    }
}
