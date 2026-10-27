using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Places;
using Dhathuru.Domain.Pricing;
using Dhathuru.Domain.Seams;
using Dhathuru.Domain.Transport;
using Dhathuru.TripEngine.Configuration;

namespace Dhathuru.TripEngine.Network;

/// <summary>Immutable search indices over one <see cref="TimetableSnapshot"/>. Built once per snapshot version.</summary>
internal sealed class TimetableNetwork
{
    private static readonly Hop[] NoHops = [];
    private static readonly FlexLink[] NoLinks = [];

    private readonly Dictionary<PlaceId, Hop[]> departures = [];
    private readonly Dictionary<PlaceId, Hop[]> arrivals = [];
    private readonly Dictionary<PlaceId, FlexLink[]> linksFrom = [];
    private readonly Dictionary<PlaceId, FlexLink[]> linksTo = [];
    private readonly Dictionary<(string Service, PlaceId From, PlaceId To), long[]> serviceDepartures = [];
    private readonly Dictionary<PlaceId, HashSet<PlaceId>> adjacency = [];

    public TimetableNetwork(TimetableSnapshot snapshot, EngineOptions options)
    {
        Version = snapshot.Version;
        Places = snapshot.Places.GroupBy(p => p.Id).ToDictionary(g => g.Key, g => g.First());

        var runs = new List<Hop>(snapshot.Runs.Count);
        foreach (var r in snapshot.Runs)
        {
            var dep = TimeMath.Minute(r.DepartAt);
            var arr = TimeMath.MinuteCeil(r.ArriveAt);
            if (arr <= dep || r.From == r.To) continue; // malformed run: ignore rather than guess
            runs.Add(Hop.FromRun(r, dep, arr));
        }
        Runs = runs;

        foreach (var g in runs.GroupBy(h => h.From))
            departures[g.Key] = g.OrderBy(h => h.Dep).ThenBy(h => h.Arr).ThenBy(h => h.Run!.ServiceRunId, StringComparer.Ordinal).ToArray();
        foreach (var g in runs.GroupBy(h => h.To))
            arrivals[g.Key] = g.OrderByDescending(h => h.Arr).ThenByDescending(h => h.Dep).ThenBy(h => h.Run!.ServiceRunId, StringComparer.Ordinal).ToArray();
        foreach (var g in runs.GroupBy(h => (h.ServiceKey, h.From, h.To)))
            serviceDepartures[g.Key] = g.Select(h => h.Dep).Order().ToArray();

        var links = new List<FlexLink>();
        foreach (var t in snapshot.Transfers)
        {
            if (t.From == t.To || t.DurationMin <= 0) continue;
            links.Add(new FlexLink(t.From, t.To, t.Mode, t.DurationMin, t.Fare, t.Pricing, Assumed: false));
        }

        // A place and its parent (a jetty and its island) are one walk apart even when no provider says so. Flagged as assumed.
        var linked = links.Select(l => (l.From, l.To)).ToHashSet();
        foreach (var p in snapshot.Places)
        {
            if (p.ParentId is not { } parent || parent == p.Id) continue;
            if (!linked.Contains((p.Id, parent)))
                links.Add(new FlexLink(p.Id, parent, TransportMode.Walk, options.Connections.ImplicitLinkMin, Money.Zero, PricingKind.Estimate, Assumed: true));
            if (!linked.Contains((parent, p.Id)))
                links.Add(new FlexLink(parent, p.Id, TransportMode.Walk, options.Connections.ImplicitLinkMin, Money.Zero, PricingKind.Estimate, Assumed: true));
        }

        foreach (var g in links.GroupBy(l => l.From))
            linksFrom[g.Key] = g.OrderBy(l => l.DurationMin).ThenBy(l => l.To.Value, StringComparer.Ordinal).ToArray();
        foreach (var g in links.GroupBy(l => l.To))
            linksTo[g.Key] = g.OrderBy(l => l.DurationMin).ThenBy(l => l.From.Value, StringComparer.Ordinal).ToArray();

        foreach (var h in runs) Connect(h.From, h.To);
        foreach (var l in links) Connect(l.From, l.To);
    }

    public string Version { get; }

    public IReadOnlyDictionary<PlaceId, SnapshotPlace> Places { get; }

    public IReadOnlyList<Hop> Runs { get; }

    /// <summary>Runs leaving a place, sorted by departure time.</summary>
    public Hop[] DeparturesFrom(PlaceId place) => departures.GetValueOrDefault(place, NoHops);

    /// <summary>Runs arriving at a place, sorted by arrival time, latest first.</summary>
    public Hop[] ArrivalsAt(PlaceId place) => arrivals.GetValueOrDefault(place, NoHops);

    public FlexLink[] LinksFrom(PlaceId place) => linksFrom.GetValueOrDefault(place, NoLinks);

    public FlexLink[] LinksTo(PlaceId place) => linksTo.GetValueOrDefault(place, NoLinks);

    /// <summary>True when no later departure of the same service on the same route leaves on the same local day.</summary>
    public bool IsLastServiceOfDay(Hop run)
    {
        if (!serviceDepartures.TryGetValue((run.ServiceKey, run.From, run.To), out var times)) return false;
        var idx = Array.BinarySearch(times, run.Dep);
        if (idx < 0) idx = ~idx;
        var date = TimeMath.LocalDate(run.Dep);
        for (var i = idx; i < times.Length; i++)
        {
            if (times[i] <= run.Dep) continue;
            if (TimeMath.LocalDate(times[i]) == date) return false;
        }
        return true;
    }

    /// <summary>Timetable-blind reachability: does any chain of services and links connect the two places at all?</summary>
    public bool IsConnected(PlaceId from, PlaceId to)
    {
        if (from == to) return true;
        var seen = new HashSet<PlaceId> { from };
        var queue = new Queue<PlaceId>();
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            if (!adjacency.TryGetValue(p, out var next)) continue;
            foreach (var n in next)
            {
                if (n == to) return true;
                if (seen.Add(n)) queue.Enqueue(n);
            }
        }
        return false;
    }

    public bool HasPlace(PlaceId id) => Places.ContainsKey(id);

    private void Connect(PlaceId a, PlaceId b)
    {
        if (!adjacency.TryGetValue(a, out var set)) adjacency[a] = set = [];
        set.Add(b);
    }
}
