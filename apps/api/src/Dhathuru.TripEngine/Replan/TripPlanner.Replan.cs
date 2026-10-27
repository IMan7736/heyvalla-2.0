using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Pricing;
using Dhathuru.Domain.Time;
using Dhathuru.Domain.Transport;
using Dhathuru.Domain.Trips;
using Dhathuru.TripEngine.Assembly;
using Dhathuru.TripEngine.Delays;
using Dhathuru.TripEngine.Network;
using Dhathuru.TripEngine.Ranking;
using Dhathuru.TripEngine.Search;
using Dhathuru.TripEngine.Support;

namespace Dhathuru.TripEngine;

public sealed partial class TripPlanner
{
    /// <summary>
    /// Safe continuations after a delay or cancellation. Confirmed legs are kept wherever they can still be made:
    /// the router looks for a way to rejoin each later confirmed leg, and for a full new route as the fallback.
    /// A delay that every later connection absorbs comes back as the original trip with updated buffers.
    /// </summary>
    public async Task<PlanResult> PlanReplanAsync(ReplanQuery q, CancellationToken ct)
    {
        var notices = new List<Explanation>();
        var searchId = StableIds.Search($"replan|{q.BookingId}|{q.Now:o}|{q.Disruption.LegId}|{q.Disruption.Kind}|{q.Disruption.DelayMinutes}");
        if (bookings is null) return Empty(searchId, notices, "BOOKING_LOOKUP_UNAVAILABLE");
        var booked = await bookings.GetAsync(q.BookingId, ct).ConfigureAwait(false);
        if (booked is null || booked.Legs.Count == 0) return Empty(searchId, notices, "BOOKING_NOT_FOUND");
        var d = booked.Legs.ToList().FindIndex(l => l.Id == q.Disruption.LegId);
        if (d < 0) return Empty(searchId, notices, "DISRUPTED_LEG_NOT_IN_BOOKING");
        var delayed = q.Disruption.Kind == DisruptionKind.Delayed;
        if (delayed && q.Disruption.DelayMinutes is not > 0) return Empty(searchId, notices, "DELAY_MINUTES_REQUIRED");
        var delayMin = q.Disruption.DelayMinutes ?? 0;

        var nowMin = TimeMath.Minute(q.Now);
        var lastArrive = booked.Legs.Max(l => l.ArriveAt);
        var fromDate = MaldivesTime.LocalDate(q.Now).AddDays(-options.SnapshotDaysBefore);
        var toDate = new[] { MaldivesTime.LocalDate(lastArrive).AddDays(1), MaldivesTime.LocalDate(q.Now).AddDays(2) }.Max();
        var snapshot = await snapshots.GetSnapshotAsync(fromDate, toDate, ct).ConfigureAwait(false);
        var network = NetworkFor(snapshot);
        var delays = new DelayModel(options.Delays, await LoadStatsAsync(network, notices, ct).ConfigureAwait(false));
        var rules = new ConnectionRules(options, booked.Passengers.Total);
        var tolerance = booked.Preferences.RiskTolerance;
        var signature = $"replan|{q.BookingId}|{nowMin}|{q.Disruption.LegId}|{q.Disruption.Kind}|{delayMin}";

        // ---- the booking as hops, with the disruption applied ------------------------------------------------------
        var cur = booked.Legs.Select(l => ToHop(l, network)).ToList();
        var originals = booked.Legs.ToArray();
        int broken = -1, frozen;
        if (delayed)
        {
            cur[d] = cur[d].Retimed(cur[d].Dep >= nowMin ? cur[d].Dep + delayMin : cur[d].Dep, cur[d].Arr + delayMin);
            for (var j = d + 1; j < cur.Count; j++)
            {
                var prev = cur[j - 1];
                var h = cur[j];
                if (!h.IsScheduled)
                {
                    var dep = Math.Max(h.Dep, prev.Arr + rules.Required(prev, h));
                    if (dep != h.Dep) cur[j] = h.Retimed(dep, dep + h.DurationMin);
                }
                else if (h.Dep - prev.Arr < rules.Required(prev, h))
                {
                    broken = j;
                    break;
                }
            }
            frozen = d + 1;
        }
        else
        {
            broken = d;
            frozen = d;
        }

        var fixedMap = new Dictionary<Hop, Leg>();
        for (var i = 0; i < cur.Count; i++) fixedMap[cur[i]] = originals[i];

        var disruption = delayed
            ? new Explanation("DISRUPTION_DELAYED", new Dictionary<string, object?> { ["minutes"] = delayMin, ["legIndex"] = d }, Severity.Caution)
            : new Explanation("DISRUPTION_CANCELLED", new Dictionary<string, object?> { ["legIndex"] = d }, Severity.Caution);

        var ctx = new AssemblyContext(options, network, delays, booked.Passengers, tolerance, snapshot.Version, null, null, nowMin);
        var assembler = new ItineraryAssembler(ctx);
        var ranker = new Ranker(options);
        var profile = options.Profile(tolerance);
        var pool = new Dictionary<string, Candidate>(StringComparer.Ordinal);

        void Add(Candidate? c, params Explanation[] extra)
        {
            // Same schedule reached two ways: keep the one that preserves more of what the traveller already bought.
            if (c is null || (pool.TryGetValue(c.Signature, out var existing) && existing.PreservedLegs >= c.PreservedLegs)) return;
            c.Explanations.InsertRange(0, extra);
            c.Explanations.Insert(0, disruption);
            var keptSuffix = c.PreservedLegs;
            if (keptSuffix > 0)
                c.Explanations.Add(new Explanation("REPLAN_KEEPS_LEGS", new Dictionary<string, object?> { ["count"] = keptSuffix }, Severity.Good));
            var changed = c.Hops.Skip(frozen).Count(h => !fixedMap.ContainsKey(h));
            if (changed > 0)
                c.Explanations.Add(new Explanation("REPLAN_NEW_LEGS", new Dictionary<string, object?> { ["count"] = changed }, Severity.Info));
            pool[c.Signature] = c;
        }

        // 1. Nothing breaks: the same trip, with the buffers the delay left.
        var absorbedOk = false;
        if (delayed && broken < 0)
        {
            var absorbed = assembler.Assemble(cur, fixedMap, frozen);
            if (absorbed is not null)
            {
                var extra = new Explanation("DELAY_ABSORBED", new Dictionary<string, object?>
                {
                    ["minutes"] = delayMin,
                    ["slackMinutes"] = absorbed.MinSlackMin == long.MaxValue ? null : absorbed.MinSlackMin,
                }, Severity.Good);
                absorbedOk = absorbed.MissProbability <= profile.MaxMissProbability
                    && (absorbed.MinSlackMin == long.MaxValue || absorbed.MinSlackMin >= profile.MinSlackMin);
                Add(absorbed, extra);
            }
        }

        // 2. Otherwise (or when the absorbed trip is now too fragile) look for continuations.
        if (!absorbedOk)
        {
            var keptThrough = delayed ? d : d - 1;
            var prefix = cur.Take(keptThrough + 1).ToList();
            var prefixLast = prefix.Count > 0 ? prefix[^1] : null;
            var loc = delayed ? cur[d].To : cur[d].From;
            var ready = nowMin;
            if (prefixLast is not null) ready = Math.Max(ready, prefixLast.Arr + rules.Alight(prefixLast));
            var excluded = new HashSet<string>(StringComparer.Ordinal) { cur[d].Key, originals[d].ServiceRunId ?? originals[d].Id.Value };
            var router = new Router(network, options, delays);
            Hop[] NoSuffix() => [];

            void Try(IReadOnlyList<Hop> middle, IReadOnlyList<Hop> suffix, Explanation? extra)
            {
                var path = prefix.Concat(middle).Concat(suffix).ToList();
                if (path.Count == 0) return;
                var c = assembler.Assemble(path, fixedMap, frozen);
                if (extra is null) Add(c); else Add(c, extra);
            }

            // Rejoin one of the later confirmed legs.
            for (var j = d + 1; j < cur.Count; j++)
            {
                var target = cur[j];
                if (!target.IsScheduled || target.Dep <= ready) continue;
                var suffix = cur.Skip(j).ToList();
                if (target.From == loc)
                {
                    Try([], suffix, null);
                    continue;
                }
                var routed = router.Search(new RouteRequest
                {
                    Origin = loc,
                    Goal = target.From,
                    AnchorMinute = ready,
                    Passengers = booked.Passengers,
                    Next = target,
                    ExcludeRuns = excluded,
                    Modes = booked.Preferences.Modes is { Count: > 0 } m ? m.ToHashSet() : null,
                });
                foreach (var rc in routed.Candidates) Try(rc.Hops, suffix, null);
            }

            // Fallback: a whole new route to the final destination.
            var destination = cur[^1].To;
            if (loc == destination)
            {
                Try([], NoSuffix(), null);
            }
            else
            {
                var routed = router.Search(new RouteRequest
                {
                    Origin = loc,
                    Goal = destination,
                    AnchorMinute = ready,
                    Passengers = booked.Passengers,
                    ExcludeRuns = excluded,
                    BudgetMinor = booked.Preferences.BudgetMax?.AmountMinor,
                    Modes = booked.Preferences.Modes is { Count: > 0 } m ? m.ToHashSet() : null,
                });
                foreach (var rc in routed.Candidates) Try(rc.Hops, NoSuffix(), new Explanation("REPLAN_FULL_ROUTE", new Dictionary<string, object?>(), Severity.Info));
            }
        }

        var anchor = delayed ? cur[d].Arr : nowMin;
        var ranked = ranker.Rank(pool.Values.OrderBy(c => c.Signature, StringComparer.Ordinal).ToList(), tolerance, false, Math.Min(anchor, pool.Count > 0 ? pool.Values.Min(c => c.DepartMin) : anchor), snapshot.Version, signature);
        foreach (var o in ranked.Options) store.Put(o);
        if (ranked.Options.Count == 0)
        {
            notices.Add(Notice("NO_SAFE_REPLAN", Severity.Caution));
            notices.Add(Notice("SUGGEST_CONTACT_OPERATOR", Severity.Info));
        }
        else if (ranked.ToleranceRelaxed)
        {
            notices.Add(Notice("NO_OPTION_WITHIN_RISK_TOLERANCE", Severity.Caution, ("tolerance", tolerance.ToString().ToLowerInvariant())));
        }
        return new PlanResult(new TripSearchResult(searchId, ranked.Options), notices);
    }

    private static Hop ToHop(Leg l, TimetableNetwork network)
    {
        var dep = TimeMath.Minute(l.DepartAt);
        var arr = Math.Max(dep + 1, TimeMath.MinuteCeil(l.ArriveAt));
        if (l.ServiceRunId is { } runId)
        {
            var run = network.Runs.FirstOrDefault(h => h.Run!.ServiceRunId == runId)?.Run;
            if (run is not null) return Hop.FromRun(run, dep, arr);
        }
        var flexible = l.ServiceRunId is null && l.Mode is TransportMode.Taxi or TransportMode.Walk;
        return new Hop
        {
            Kind = flexible ? HopKind.Flexible : HopKind.Scheduled,
            Mode = l.Mode,
            From = l.From.Id,
            To = l.To.Id,
            Dep = dep,
            Arr = arr,
            Fare = Money.Zero,
            Pricing = l.Pricing,
            Assumed = l.NoteCodes.Contains("ASSUMED_LINK"),
            Synthetic = l.NoteCodes.Contains("SYNTHETIC_TIMETABLE"),
            ServiceKey = l.ServiceId?.Value ?? l.ServiceRunId ?? l.Id.Value,
            ProviderId = l.ProviderId,
            RunKey = flexible ? null : l.ServiceRunId ?? l.Id.Value,
        };
    }
}
