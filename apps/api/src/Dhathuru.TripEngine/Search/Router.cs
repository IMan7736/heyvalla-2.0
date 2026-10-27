using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Pricing;
using Dhathuru.Domain.Seams;
using Dhathuru.Domain.Transport;
using Dhathuru.Domain.Trips;
using Dhathuru.TripEngine.Configuration;
using Dhathuru.TripEngine.Network;
using Dhathuru.TripEngine.Delays;
using Dhathuru.TripEngine.Support;

namespace Dhathuru.TripEngine.Search;

internal sealed record RouteRequest
{
    public required PlaceId Origin { get; init; }

    public required PlaceId Goal { get; init; }

    /// <summary>Forward: the moment the traveller is at the origin. Backward: the deadline at the goal. Epoch minutes.</summary>
    public required long AnchorMinute { get; init; }

    public bool Backward { get; init; }

    public required Passengers Passengers { get; init; }

    public int? MaxTransfers { get; init; }

    public long? BudgetMinor { get; init; }

    public IReadOnlySet<TransportMode>? Modes { get; init; }

    /// <summary>Forward only: a fixed scheduled leg that must be boarded after reaching the goal (re-plan rejoin).</summary>
    public Hop? Next { get; init; }

    /// <summary>Forward only: hard deadline for reaching the goal; its delay risk is included.</summary>
    public long? Deadline { get; init; }

    /// <summary>Runs that must not be used (a cancelled or delayed run during a re-plan), by <see cref="Hop.Key"/>.</summary>
    public IReadOnlySet<string>? ExcludeRuns { get; init; }
}

internal sealed record RouteCandidate(IReadOnlyList<Hop> Hops, long Cost, double Survival);

internal sealed class RouteResult
{
    public List<RouteCandidate> Candidates { get; } = [];

    public int RejectedTooTight { get; set; }

    public int RejectedTaxiClosed { get; set; }

    public int RejectedBudget { get; set; }

    public int RejectedTransfers { get; set; }

    public bool Truncated { get; set; }
}

/// <summary>
/// Multi-criteria label-setting search over the timetable. Criteria: time, price, miss risk, legs.
/// Backward search (arrive-by) is the same code run on mirrored time; the connection rules are always asked in real order.
/// </summary>
internal sealed class Router
{
    private const int PopLimit = 400_000;
    private const double Eps = 1e-12;

    private readonly TimetableNetwork network;
    private readonly EngineOptions options;
    private readonly DelayModel delays;
    private readonly Dictionary<string, DelayDist> runDelay = [];
    private readonly FareCalculator fares;

    public Router(TimetableNetwork network, EngineOptions options, DelayModel delays)
    {
        this.network = network;
        this.options = options;
        this.delays = delays;
        fares = new FareCalculator(options);
    }

    public RouteResult Search(RouteRequest req)
    {
        var bwd = req.Backward;
        var rules = new ConnectionRules(options, req.Passengers.Total);
        var result = new RouteResult();
        var start = bwd ? req.Goal : req.Origin;
        var target = bwd ? req.Origin : req.Goal;
        if (start == target) return result;

        var t0 = bwd ? -req.AnchorMinute : req.AnchorMinute;
        var limit = t0 + options.HorizonMin;
        if (req.Deadline is { } dl) limit = Math.Min(limit, dl);
        if (req.Next is { } nx) limit = Math.Min(limit, nx.Dep);

        var buckets = new Dictionary<(PlaceId, TransportMode?, string?, bool), List<Label>>();
        var queue = new PriorityQueue<Label, (long, long, int)>();
        var nextId = 0;

        Label root = new() { Id = nextId++, Place = start, T = t0, Surv = 1 };
        Insert(root);

        var pops = 0;
        var bestGoal = long.MaxValue;
        while (queue.TryDequeue(out var label, out _))
        {
            if (label.Dead) continue;
            if (++pops > PopLimit) { result.Truncated = true; break; }
            if (label.Place == target) { bestGoal = Math.Min(bestGoal, label.T); continue; }
            if (bestGoal != long.MaxValue && label.T > bestGoal + options.AlternativeWindowMin) continue;
            ExpandScheduled(label);
            ExpandFlexible(label);
        }

        foreach (var list in buckets.Values)
        {
            foreach (var l in list)
            {
                if (l.Dead || l.Place != target || l.Parent is null) continue;
                var hops = new List<Hop>();
                for (var c = l; c.Via is not null; c = c.Parent!) hops.Add(c.Via);
                if (!bwd) hops.Reverse();
                result.Candidates.Add(new RouteCandidate(hops, l.Cost, l.Surv));
            }
        }
        result.Candidates.Sort((a, b) =>
        {
            var c = a.Cost.CompareTo(b.Cost);
            return c != 0 ? c : string.CompareOrdinal(Signature(a), Signature(b));
        });
        return result;

        // ---- expansion --------------------------------------------------------------------------------------------

        void ExpandScheduled(Label label)
        {
            if (label.Legs >= options.MaxLegs) return;
            var list = bwd ? network.ArrivalsAt(label.Place) : network.DeparturesFrom(label.Place);
            var maxDep = label.Last is null ? limit : Math.Min(limit, label.T + options.MaxConnectionWaitMin);
            var i = LowerBound(list, label.T, bwd);
            var taken = new Dictionary<(string, PlaceId), int>();
            for (; i < list.Length; i++)
            {
                var h = list[i];
                var sDep = bwd ? -h.Arr : h.Dep;
                if (sDep > maxDep) break;
                var sArr = bwd ? -h.Dep : h.Arr;
                if (sArr > limit) continue;
                if (req.Modes is { } modes && !modes.Contains(h.Mode)) continue;
                if (req.ExcludeRuns is { } excluded && excluded.Contains(h.Key)) continue;

                var sTo = bwd ? h.From : h.To;
                if (Visited(label, sTo)) continue;
                if (req.MaxTransfers is { } mt && label.Legs > mt) { result.RejectedTransfers++; continue; }

                var required = Required(label.Last, h);
                var gap = sDep - label.T;
                if (gap < required) { if (gap >= 0) result.RejectedTooTight++; continue; }

                var tk = (h.ServiceKey, sTo);
                taken.TryGetValue(tk, out var n);
                if (n >= options.RunsPerService) continue;
                taken[tk] = n + 1;

                var cost = label.Cost + fares.PartyFare(h, req.Passengers);
                if (req.BudgetMinor is { } budget && cost > budget) { result.RejectedBudget++; continue; }

                var p = Miss(label, h, gap - required);
                var next = new Label
                {
                    Id = nextId++,
                    Parent = label,
                    Via = h,
                    Place = sTo,
                    T = sArr,
                    Cost = cost,
                    Surv = label.Surv * (1 - p),
                    Legs = label.Legs + 1,
                    Last = h,
                    LastSched = h,
                    Flex = DelayDist.None,
                };
                Offer(next);
            }
        }

        void ExpandFlexible(Label label)
        {
            if (label.Legs >= options.MaxLegs || (label.Last is { Kind: HopKind.Flexible })) return;
            var links = bwd ? network.LinksTo(label.Place) : network.LinksFrom(label.Place);
            foreach (var link in links)
            {
                var sTo = bwd ? link.From : link.To;
                if (Visited(label, sTo)) continue;
                if (req.MaxTransfers is { } mt && label.Legs > mt) { result.RejectedTransfers++; continue; }

                var stub = new Hop
                {
                    Kind = HopKind.Flexible, Mode = link.Mode, From = link.From, To = link.To, Dep = 0, Arr = 0,
                    Fare = link.Fare, Pricing = link.Pricing, Assumed = link.Assumed,
                };
                var sDep = label.T + Required(label.Last, stub);
                if (link.Mode == TransportMode.Taxi)
                {
                    // Taxis keep service hours: wait for them to open (forward) or arrive before they close (backward).
                    long? shifted;
                    if (bwd)
                    {
                        var arrReal = rules.PrevTaxiArrival(-sDep, link.DurationMin);
                        shifted = arrReal is { } a ? -a : null;
                    }
                    else
                    {
                        shifted = rules.NextTaxiDeparture(sDep, link.DurationMin);
                    }
                    if (shifted is not { } fit) { result.RejectedTaxiClosed++; continue; }
                    if (fit != sDep) result.RejectedTaxiClosed++;
                    sDep = fit;
                }
                var sArr = sDep + link.DurationMin;
                if (sArr > limit) continue;
                if (label.Last is not null && sDep - label.T > options.MaxConnectionWaitMin) continue;
                var dep = bwd ? -sArr : sDep;
                var arr = bwd ? -sDep : sArr;

                var hop = stub.Retimed(dep, arr);
                var cost = label.Cost + fares.PartyFare(hop, req.Passengers);
                if (req.BudgetMinor is { } budget && cost > budget) { result.RejectedBudget++; continue; }

                var next = new Label
                {
                    Id = nextId++,
                    Parent = label,
                    Via = hop,
                    Place = sTo,
                    T = sArr,
                    Cost = cost,
                    Surv = label.Surv,
                    Legs = label.Legs + 1,
                    Last = hop,
                    LastSched = label.LastSched,
                    Flex = DelayDist.Merge(label.Flex, delays.ForFlexible(link.Mode, link.DurationMin)),
                };
                Offer(next);
            }
        }

        // ---- helpers ----------------------------------------------------------------------------------------------

        int Required(Hop? last, Hop cand) => bwd ? rules.Required(cand, last) : rules.Required(last, cand);

        double Miss(Label label, Hop s, long slack)
        {
            // Delay of the real-earlier scheduled leg plus the flexible legs between it and the real-later one.
            var earlier = bwd ? DelayOf(s) : label.LastSched is null ? DelayDist.None : DelayOf(label.LastSched);
            return DelayDist.SumExceeds(earlier, label.Flex, slack);
        }

        DelayDist DelayOf(Hop h)
        {
            if (runDelay.TryGetValue(h.ServiceKey, out var d)) return d;
            return runDelay[h.ServiceKey] = delays.ForRun(h.Mode, h.ServiceKey);
        }

        void Offer(Label l)
        {
            if (l.Place == target && !CloseAtGoal(l)) return;
            Insert(l);
        }

        // Forward re-plan: the goal is followed by a fixed leg and/or a deadline. Check them with the same rules.
        bool CloseAtGoal(Label l)
        {
            if (bwd) return true;
            var surv = l.Surv;
            if (req.Next is { } next)
            {
                var required = rules.Required(l.Last, next);
                var gap = next.Dep - l.T;
                if (gap < required) { if (gap >= 0) result.RejectedTooTight++; return false; }
                var earlier = l.LastSched is null ? DelayDist.None : DelayOf(l.LastSched);
                surv *= 1 - DelayDist.SumExceeds(earlier, l.Flex, gap - required);
            }
            if (req.Deadline is { } deadline)
            {
                if (l.T > deadline) return false;
                var earlier = l.LastSched is null ? DelayDist.None : DelayOf(l.LastSched);
                surv *= 1 - DelayDist.SumExceeds(earlier, l.Flex, deadline - l.T);
            }
            l.Surv = surv;
            return true;
        }

        void Insert(Label l)
        {
            var key = (l.Place, l.Last?.Mode, l.LastSched?.ServiceKey, l.Last is { Kind: HopKind.Flexible });
            if (!buckets.TryGetValue(key, out var list)) buckets[key] = list = [];
            foreach (var o in list)
            {
                if (!o.Dead && Dominates(o, l)) return;
            }
            foreach (var o in list)
            {
                if (!o.Dead && Dominates(l, o)) o.Dead = true;
            }
            list.RemoveAll(o => o.Dead);
            if (list.Count >= options.MaxLabelsPerKey)
            {
                var worst = list.MaxBy(o => (o.T, o.Cost, o.Id))!;
                if (l.T >= worst.T) return;
                worst.Dead = true;
                list.Remove(worst);
            }
            list.Add(l);
            queue.Enqueue(l, (l.T, l.Cost, l.Id));
        }
    }

    public static string Signature(RouteCandidate c) =>
        string.Join('|', c.Hops.Select(h => h.Key));

    private static bool Dominates(Label a, Label b) =>
        a.T <= b.T && a.Cost <= b.Cost && a.Surv >= b.Surv - Eps && a.Legs <= b.Legs;

    private static bool Visited(Label l, PlaceId place)
    {
        for (var c = l; c is not null; c = c.Parent)
        {
            if (c.Place == place) return true;
        }
        return false;
    }

    private static int LowerBound(Hop[] list, long searchT, bool bwd)
    {
        int lo = 0, hi = list.Length;
        while (lo < hi)
        {
            var mid = (lo + hi) >>> 1;
            var t = bwd ? -list[mid].Arr : list[mid].Dep;
            if (t < searchT) lo = mid + 1; else hi = mid;
        }
        return lo;
    }

    private sealed class Label
    {
        public int Id { get; init; }

        public Label? Parent { get; init; }

        public Hop? Via { get; init; }

        public PlaceId Place { get; init; }

        public long T { get; init; }

        public long Cost { get; init; }

        public double Surv { get; set; } = 1;

        public int Legs { get; init; }

        public Hop? Last { get; init; }

        public Hop? LastSched { get; init; }

        public DelayDist Flex { get; init; }

        public bool Dead { get; set; }
    }
}
