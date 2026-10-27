using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Trips;
using Dhathuru.TripEngine;
using Dhathuru.TripEngine.Fixtures;

// Trip engine benchmarks on the synthetic national network.
//   dotnet run -c Release --project tests/Dhathuru.TripEngine.Benchmarks -- --filter "*"
// "p95" mode (no BenchmarkDotNet): dotnet run -c Release --project ... -- p95

if (args.Contains("p95"))
{
    P95.Run();
    return;
}
BenchmarkSwitcher.FromAssembly(typeof(SearchBenchmarks).Assembly).Run(args);

public class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

public static class Scenario
{
    public static readonly DateTimeOffset Now = new(2026, 10, 26, 7, 0, 0, TimeSpan.Zero);

    public static (TripPlanner Planner, NationalWorld World) Create(int density)
    {
        var world = new NationalWorld(1, density);
        var planner = new TripPlanner(new StaticSnapshotProvider(world.World.Build()), new StaticReliability(), null, null, null, new FixedClock(Now));
        return (planner, world);
    }

    /// <summary>Deterministic spread of queries: island to island across the country, both time modes, three tolerances.</summary>
    public static List<TripSearchQuery> Queries(NationalWorld world, int count)
    {
        var r = new Random(7);
        var list = new List<TripSearchQuery>();
        while (list.Count < count)
        {
            var from = world.Islands[r.Next(world.Islands.Count)];
            var to = world.Islands[r.Next(world.Islands.Count)];
            if (from == to) continue;
            var time = new TimeOnly(r.Next(5, 20), r.Next(0, 4) * 15);
            var mode = r.Next(2) == 0 ? TimeMode.DepartAfter : TimeMode.ArriveBy;
            list.Add(new TripSearchQuery(from, to, new DateOnly(2026, 10, 27), time, mode,
                new Passengers(r.Next(1, 4)), new TripPreferences((RiskTolerance)r.Next(3))));
        }
        return list;
    }
}

[MemoryDiagnoser]
public class SearchBenchmarks
{
    private TripPlanner planner = null!;
    private List<TripSearchQuery> queries = null!;

    [Params(1, 3)]
    public int Density { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        (planner, var world) = Scenario.Create(Density);
        queries = Scenario.Queries(world, 40);
    }

    [Benchmark(Description = "40 national searches")]
    public async Task<int> Search()
    {
        var total = 0;
        foreach (var q in queries) total += (await planner.PlanAsync(q, CancellationToken.None)).Options.Count;
        return total;
    }
}

public static class P95
{
    public static void Run()
    {
        foreach (var density in new[] { 1, 3 })
        {
            var (planner, world) = Scenario.Create(density);
            var queries = Scenario.Queries(world, 400);
            foreach (var q in queries.Take(40)) planner.PlanAsync(q, CancellationToken.None).GetAwaiter().GetResult(); // warm-up
            var times = new List<double>();
            var empty = 0;
            foreach (var q in queries)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var plan = planner.PlanAsync(q, CancellationToken.None).GetAwaiter().GetResult();
                sw.Stop();
                times.Add(sw.Elapsed.TotalMilliseconds);
                if (plan.Options.Count == 0) empty++;
            }
            times.Sort();
            double Pct(double p) => times[(int)Math.Min(times.Count - 1, Math.Ceiling(p * times.Count) - 1)];
            Console.WriteLine($"density {density}: {world.RunCount} runs over 3 days, {queries.Count} searches ({empty} empty): p50 {Pct(.5):F1} ms, p95 {Pct(.95):F1} ms, p99 {Pct(.99):F1} ms, max {times[^1]:F1} ms");
        }
    }
}
