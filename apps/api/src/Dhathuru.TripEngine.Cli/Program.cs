using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Places;
using Dhathuru.Domain.Time;
using Dhathuru.Domain.Trips;
using Dhathuru.TripEngine;
using Dhathuru.TripEngine.Fixtures;

// Debug harness for the trip engine over the built-in synthetic worlds. Not shipped; not part of the API.
//   dotnet run --project src/Dhathuru.TripEngine.Cli -- --from Maafushi --to Kooddoo --date 2026-10-27 --time 05:00
//   options: --world mini|national  --mode departAfter|arriveBy  --tolerance cautious|balanced|relaxed  --pax 2  --list

var opt = Args.Parse(args);
var world = opt.Get("world", "mini");
WorldBuilder builder;
string defaultFrom, defaultTo;
if (world == "national")
{
    var nat = new NationalWorld(1);
    builder = nat.World;
    defaultFrom = "Atoll 07 Island 03";
    defaultTo = "Atoll 12 Island 05";
}
else
{
    builder = new MiniMaldives().World;
    defaultFrom = "Maafushi";
    defaultTo = "Kooddoo";
}

var snapshot = builder.Build();
if (opt.Has("list"))
{
    foreach (var p in snapshot.Places) Console.WriteLine($"{p.Type,-8} {p.Name}");
    return 0;
}

PlaceId Find(string name)
{
    var hit = snapshot.Places.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
        ?? snapshot.Places.FirstOrDefault(p => p.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
    return hit?.Id ?? throw new ArgumentException($"No place matches '{name}'. Use --list.");
}

var from = Find(opt.Get("from", defaultFrom));
var to = Find(opt.Get("to", defaultTo));
var date = DateOnly.Parse(opt.Get("date", "2026-10-27"));
var time = TimeOnly.Parse(opt.Get("time", "05:00"));
var mode = opt.Get("mode", "departAfter").Equals("arriveBy", StringComparison.OrdinalIgnoreCase) ? TimeMode.ArriveBy : TimeMode.DepartAfter;
var tolerance = Enum.Parse<RiskTolerance>(opt.Get("tolerance", "balanced"), ignoreCase: true);
var pax = int.Parse(opt.Get("pax", "1"));

var planner = new TripPlanner(
    new StaticSnapshotProvider(snapshot), new StaticReliability(), null, null, null,
    new FixedClock(new DateTimeOffset(2026, 10, 26, 7, 0, 0, TimeSpan.Zero)));
var query = new TripSearchQuery(from, to, date, time, mode, new Passengers(pax), new TripPreferences(tolerance));

var sw = System.Diagnostics.Stopwatch.StartNew();
var plan = await planner.PlanAsync(query, CancellationToken.None);
sw.Stop();

string Name(PlaceRef p) => p.Name["en"];
string Clock(DateTimeOffset t) => MaldivesTime.ToLocal(t).ToString("ddd HH:mm");

Console.WriteLine($"{opt.Get("from", defaultFrom)} -> {opt.Get("to", defaultTo)}  {mode} {date:yyyy-MM-dd} {time:HH\\:mm}  {tolerance}  x{pax}   ({sw.ElapsedMilliseconds} ms, {plan.Options.Count} options)");
foreach (var o in plan.Options)
{
    Console.WriteLine();
    Console.WriteLine($"[{string.Join(", ", o.Labels)}]  {Clock(o.DepartAt)} -> {Clock(o.ArriveAt)}  {o.DurationMin} min  {o.Price.Total}  risk {o.Risk.MissProbability:P1} ({o.Risk.Level})  score {o.Score}");
    for (var i = 0; i < o.Legs.Count; i++)
    {
        var l = o.Legs[i];
        Console.WriteLine($"   {Clock(l.DepartAt)}-{MaldivesTime.ToLocal(l.ArriveAt):HH:mm}  {l.Mode,-14} {Name(l.From)} -> {Name(l.To)}  {l.Price}{(l.NoteCodes.Count > 0 ? "  [" + string.Join(",", l.NoteCodes) + "]" : "")}");
        if (i < o.Buffers.Count)
        {
            var b = o.Buffers[i];
            Console.WriteLine($"       buffer {b.Minutes} min (needs {b.RequiredMinutes}, comfortable {b.ComfortMinutes})");
        }
    }
    Console.WriteLine("   why: " + string.Join(", ", o.Explanations.Select(e => e.Params.Count == 0 ? e.Code : $"{e.Code}{{{string.Join(",", e.Params.Select(p => $"{p.Key}={p.Value}"))}}}")));
    if (o.Warnings.Count > 0) Console.WriteLine("   warn: " + string.Join(", ", o.Warnings.Select(w => w.Code)));
}
if (plan.Notices.Count > 0)
{
    Console.WriteLine();
    Console.WriteLine("notices: " + string.Join(", ", plan.Notices.Select(n => n.Params.Count == 0 ? n.Code : $"{n.Code}{{{string.Join(",", n.Params.Select(p => $"{p.Key}={p.Value}"))}}}")));
}
return 0;

internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal sealed class Args(Dictionary<string, string> values)
{
    public static Args Parse(string[] args)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal)) continue;
            var key = args[i][2..];
            d[key] = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal) ? args[++i] : "true";
        }
        return new Args(d);
    }

    public string Get(string key, string fallback) => values.TryGetValue(key, out var v) ? v : fallback;

    public bool Has(string key) => values.ContainsKey(key);
}
