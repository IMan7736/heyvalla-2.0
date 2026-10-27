using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Places;
using Dhathuru.Domain.Pricing;
using Dhathuru.Domain.Transport;
using Dhathuru.Domain.Trips;
using Buffer = Dhathuru.Domain.Trips.Buffer;
using Dhathuru.TripEngine.Configuration;
using Dhathuru.TripEngine.Network;
using Dhathuru.TripEngine.Delays;
using Dhathuru.TripEngine.Support;

namespace Dhathuru.TripEngine.Assembly;

internal sealed record AssemblyContext(
    EngineOptions Options,
    TimetableNetwork Network,
    DelayModel Delays,
    Passengers Passengers,
    RiskTolerance Tolerance,
    string SnapshotVersion,
    long? StartAt,
    long? Deadline,
    long? NotBefore = null);

/// <summary>
/// Turns a router path into a final itinerary and is the independent safety net: it re-times flexible legs, re-checks
/// every connection against the hard floor and recomputes risk from scratch. Whatever it rejects is never shown.
/// </summary>
internal sealed class ItineraryAssembler
{
    private readonly AssemblyContext ctx;
    private readonly ConnectionRules rules;
    private readonly FareCalculator fares;
    private readonly ToleranceProfile profile;

    public ItineraryAssembler(AssemblyContext ctx)
    {
        this.ctx = ctx;
        rules = new ConnectionRules(ctx.Options, ctx.Passengers.Total);
        fares = new FareCalculator(ctx.Options);
        profile = ctx.Options.Profile(ctx.Tolerance);
    }

    /// <param name="fixedLegs">Hops that stand for already-confirmed legs: kept as they are, original ids and prices reused.</param>
    /// <param name="frozenLegs">Legs already travelled or under way (re-plan): validated, but their connections carry no future risk.</param>
    public Candidate? Assemble(IReadOnlyList<Hop> path, IReadOnlyDictionary<Hop, Leg>? fixedLegs = null, int frozenLegs = 0)
    {
        if (path.Count == 0) return null;
        var hops = Retime(path, fixedLegs);
        if (hops is null) return null;
        if (!Validate(hops, fixedLegs)) return null;

        var n = hops.Count;
        var explanations = new List<Explanation>();
        var warnings = new List<TripWarning>();

        // ---- connections and risk ------------------------------------------------------------------------------
        var survival = 1.0;
        double worstP = -1;
        int? weakest = null;
        var minSlack = long.MaxValue;
        long minBuffer = long.MaxValue;
        var buffers = new List<Buffer>(Math.Max(0, n - 1));
        var comfortExtra = new long[n];

        for (var j = 0; j < n; j++)
        {
            if (!hops[j].IsScheduled || j < frozenLegs) continue;
            var chain = Chain(hops, j);
            if (chain is null) continue;
            var (earlier, flex, slack, hasEarlier) = chain.Value;
            var p = DelayDist.SumExceeds(earlier, flex, slack);
            survival *= 1 - p;
            minSlack = Math.Min(minSlack, slack);
            comfortExtra[j] = (long)Math.Ceiling(Math.Max(profile.ComfortMarginMin, DelayDist.Merge(earlier, flex).Quantile(0.10)));
            if (hasEarlier && j > 0)
            {
                minBuffer = Math.Min(minBuffer, hops[j].Dep - hops[j - 1].Arr);
                if (p > worstP) { worstP = p; weakest = j - 1; }
            }
        }

        long? tailSlack = null;
        if (ctx.Deadline is { } deadline)
        {
            var tail = TailChain(hops, deadline);
            if (tail is { } t)
            {
                survival *= 1 - DelayDist.SumExceeds(t.Earlier, t.Flex, t.Slack);
                minSlack = Math.Min(minSlack, t.Slack);
                tailSlack = t.Slack;
            }
        }

        for (var i = 0; i + 1 < n; i++)
        {
            var req = rules.Required(hops[i], hops[i + 1]);
            var comfort = req + (hops[i + 1].IsScheduled ? comfortExtra[i + 1] : 0);
            buffers.Add(new Buffer(i, (int)(hops[i + 1].Dep - hops[i].Arr), req, (int)comfort));
        }

        // ---- legs, price ---------------------------------------------------------------------------------------
        var legs = new List<Leg>(n);
        var breakdown = new List<PriceBreakdownItem>(n);
        long total = 0;
        for (var i = 0; i < n; i++)
        {
            var leg = BuildLeg(hops[i], i, fixedLegs);
            legs.Add(leg);
            total += leg.Price.AmountMinor;
            if (leg.Mode != TransportMode.Walk)
                breakdown.Add(new PriceBreakdownItem(FareCalculator.BreakdownCode(leg.Mode), leg.Price, leg.Id));
        }

        // ---- explanations and warnings -------------------------------------------------------------------------
        Describe(hops, legs, buffers, minSlack, minBuffer, tailSlack, explanations, warnings);

        var miss = Math.Clamp(1 - survival, 0, 1);
        return new Candidate
        {
            Hops = hops,
            Legs = legs,
            Buffers = buffers,
            Price = new TripPrice(new Money(total), breakdown),
            TotalMinor = total,
            MissProbability = miss,
            WeakestConnection = weakest,
            MinSlackMin = minSlack,
            DepartMin = hops[0].Dep,
            ArriveMin = hops[^1].Arr,
            Transfers = n - 1,
            Explanations = explanations,
            Warnings = warnings,
            Signature = string.Join('|', hops.Select(h => h.Key)),
            ServicePattern = string.Join('|', hops.Select(h => h.IsScheduled ? h.ServiceKey : h.Mode.ToString())),
            ScheduledDepartures = hops.Where(h => h.IsScheduled).Select(h => h.Dep).ToList(),
            PreservedLegs = fixedLegs is null ? 0 : hops.Skip(frozenLegs).Count(fixedLegs.ContainsKey),
        };
    }

    // ---- retiming ----------------------------------------------------------------------------------------------

    /// <summary>
    /// Scheduled legs are fixed by the timetable. Flexible legs (taxi, walk) are on demand, so they are placed again here:
    /// as soon as the previous leg allows, or, when the next scheduled leg is far away, so the traveller arrives with the
    /// comfortable margin instead of idling at the destination. The delay risk is identical either way (total slack is conserved).
    /// </summary>
    private List<Hop>? Retime(IReadOnlyList<Hop> path, IReadOnlyDictionary<Hop, Leg>? fixedLegs)
    {
        var hops = path.ToList();
        for (var i = 0; i < hops.Count; i++)
        {
            var h = hops[i];
            if (h.IsScheduled || (fixedLegs?.ContainsKey(h) ?? false)) continue;
            var prev = i > 0 ? hops[i - 1] : null;
            var next = i + 1 < hops.Count ? hops[i + 1] : null;
            var dur = h.DurationMin;

            long? earliest = prev is not null
                ? prev.Arr + rules.Required(prev, h)
                : ctx.StartAt is { } s ? s + rules.Required(null, h) : null;

            long dep;
            if (next is not null)
            {
                var latest = next.Dep - rules.Required(h, next) - dur;
                var comfortable = latest - profile.ComfortMarginMin;
                if (earliest is { } e && e > latest) return null;
                dep = earliest is { } e2 ? Math.Max(e2, comfortable) : comfortable;
            }
            else
            {
                if (earliest is not { } e) return null;
                dep = e;
            }

            if (ctx.NotBefore is { } nb) dep = Math.Max(dep, nb);
            if (next is not null && dep + dur > next.Dep - rules.Required(h, next)) return null;
            if (h.Mode == TransportMode.Taxi)
            {
                if (rules.NextTaxiDeparture(dep, dur) is not { } open) return null;
                dep = open;
                if (next is not null && dep + dur > next.Dep - rules.Required(h, next)) return null;
            }
            hops[i] = h.Retimed(dep, dep + dur);
        }
        return hops;
    }

    private bool Validate(List<Hop> hops, IReadOnlyDictionary<Hop, Leg>? fixedKeys)
    {
        foreach (var h in hops)
        {
            if (h.Arr <= h.Dep) return false;
        }
        if (ctx.NotBefore is { } notBefore && hops.Any(h => !(fixedKeys?.ContainsKey(h) ?? false) && h.Dep < notBefore)) return false;
        if (ctx.NotBefore is { } nowMin && !(fixedKeys?.ContainsKey(hops[0]) ?? false) && hops[0].Dep < nowMin + rules.Required(null, hops[0])) return false;
        if (ctx.StartAt is { } start && hops[0].Dep < start + rules.Required(null, hops[0])) return false;
        for (var i = 1; i < hops.Count; i++)
        {
            if (hops[i].From != hops[i - 1].To) return false;
            if (hops[i].Dep - hops[i - 1].Arr < rules.Required(hops[i - 1], hops[i])) return false;
        }
        if (ctx.Deadline is { } deadline && hops[^1].Arr > deadline) return false;
        return true;
    }

    // ---- risk chains -------------------------------------------------------------------------------------------

    /// <summary>
    /// For scheduled leg <paramref name="j"/>: the delay of the previous scheduled leg, the noise of flexible legs between,
    /// and the slack (minutes beyond every required buffer and ride time). Null when the chain starts at an unanchored origin.
    /// </summary>
    private (DelayDist Earlier, DelayDist Flex, long Slack, bool HasEarlier)? Chain(List<Hop> hops, int j)
    {
        var k = j - 1;
        while (k >= 0 && !hops[k].IsScheduled) k--;
        DelayDist earlier;
        long start;
        Hop? prev;
        if (k >= 0)
        {
            earlier = ctx.Delays.ForRun(hops[k].Mode, hops[k].ServiceKey);
            start = hops[k].Arr;
            prev = hops[k];
        }
        else
        {
            if (ctx.StartAt is not { } s) return null;
            earlier = DelayDist.None;
            start = s;
            prev = null;
        }

        var flex = DelayDist.None;
        long required = 0, ride = 0;
        for (var m = k + 1; m <= j; m++)
        {
            required += rules.Required(prev, hops[m]);
            if (m < j)
            {
                ride += hops[m].DurationMin;
                flex = DelayDist.Merge(flex, ctx.Delays.ForFlexible(hops[m].Mode, hops[m].DurationMin));
            }
            prev = hops[m];
        }
        return (earlier, flex, hops[j].Dep - start - required - ride, k >= 0);
    }

    private (DelayDist Earlier, DelayDist Flex, long Slack)? TailChain(List<Hop> hops, long deadline)
    {
        var k = hops.Count - 1;
        while (k >= 0 && !hops[k].IsScheduled) k--;
        if (k < 0) return null;
        var flex = DelayDist.None;
        long required = 0, ride = 0;
        var prev = hops[k];
        for (var m = k + 1; m < hops.Count; m++)
        {
            required += rules.Required(prev, hops[m]);
            ride += hops[m].DurationMin;
            flex = DelayDist.Merge(flex, ctx.Delays.ForFlexible(hops[m].Mode, hops[m].DurationMin));
            prev = hops[m];
        }
        var earlier = ctx.Delays.ForRun(hops[k].Mode, hops[k].ServiceKey);
        return (earlier, flex, deadline - hops[k].Arr - required - ride);
    }

    // ---- output construction -----------------------------------------------------------------------------------

    private Leg BuildLeg(Hop h, int index, IReadOnlyDictionary<Hop, Leg>? fixedLegs)
    {
        if (fixedLegs is not null && fixedLegs.TryGetValue(h, out var original))
        {
            return original with
            {
                DepartAt = TimeMath.Instant(h.Dep),
                ArriveAt = TimeMath.Instant(h.Arr),
            };
        }

        var notes = new List<string>();
        if (h.Pricing == PricingKind.Estimate && h.Mode == TransportMode.Taxi) notes.Add("ESTIMATED_TAXI_FARE");
        else if (h.Pricing == PricingKind.Estimate && h.Mode != TransportMode.Walk) notes.Add("ESTIMATED_FARE");
        if (h.Assumed) notes.Add("ASSUMED_LINK");
        if (h.Synthetic) notes.Add("SYNTHETIC_TIMETABLE");
        if (h.IsScheduled && ctx.Network.IsLastServiceOfDay(h)) notes.Add("LAST_SERVICE_OF_DAY");

        var id = StableIds.Leg($"{ctx.SnapshotVersion}|{h.Key}|{h.Dep}|{ctx.Passengers.Total}");
        var run = h.Run;
        return new Leg(
            id,
            h.Mode,
            h.IsScheduled ? h.ProviderId : h.Mode == TransportMode.Taxi ? "flex-taxi" : "flex-walk",
            Ref(h.From),
            Ref(h.To),
            run is not null ? run.DepartAt : TimeMath.Instant(h.Dep),
            run is not null ? run.ArriveAt : TimeMath.Instant(h.Arr),
            new Money(fares.PartyFare(h, ctx.Passengers)),
            Bookable: h.IsScheduled,
            h.Pricing,
            notes,
            run?.ServiceId,
            run?.ServiceRunId,
            id.Value);
    }

    private PlaceRef Ref(PlaceId id)
    {
        if (ctx.Network.Places.TryGetValue(id, out var p))
            return new PlaceRef(id, p.Type, new Dictionary<string, string> { ["en"] = p.Name }, p.Geo);
        return new PlaceRef(id, id.Type, new Dictionary<string, string> { ["en"] = id.Value }, new GeoPoint(0, 0));
    }

    private void Describe(
        List<Hop> hops,
        List<Leg> legs,
        List<Buffer> buffers,
        long minSlack,
        long minBuffer,
        long? tailSlack,
        List<Explanation> explanations,
        List<TripWarning> warnings)
    {
        static Dictionary<string, object?> P(params (string, object?)[] kv) => kv.ToDictionary(x => x.Item1, x => x.Item2);

        if (minBuffer != long.MaxValue)
        {
            explanations.Add(minSlack >= profile.ComfortMarginMin
                ? new Explanation("BUFFER_COMFORTABLE", P(("minutes", minBuffer)), Severity.Good)
                : new Explanation("BUFFER_TIGHT", P(("minutes", minBuffer)), Severity.Caution));
        }
        if (tailSlack is { } ts && ts < profile.ComfortMarginMin)
            explanations.Add(new Explanation("ARRIVAL_DEADLINE_TIGHT", P(("minutes", ts)), Severity.Caution));

        for (var i = 0; i < hops.Count; i++)
        {
            var h = hops[i];
            if (!h.IsScheduled)
            {
                if (h.Mode == TransportMode.Taxi && h.Pricing == PricingKind.Estimate)
                    warnings.Add(new TripWarning("ESTIMATED_TAXI_FARE", P(("legIndex", i))));
                if (h.Assumed)
                    warnings.Add(new TripWarning("ASSUMED_LINK", P(("legIndex", i))));
                continue;
            }
            if (h.Synthetic) warnings.Add(new TripWarning("SYNTHETIC_TIMETABLE", P(("legIndex", i))));
            if (legs[i].NoteCodes.Contains("LAST_SERVICE_OF_DAY"))
                explanations.Add(new Explanation("LAST_SERVICE_OF_DAY", P(("mode", ModeCode(h.Mode)), ("legIndex", i)), Severity.Caution));
            if (h.Mode is TransportMode.DomesticFlight or TransportMode.Seaplane && i > 0)
            {
                var cutoff = rules.Boarding(h);
                var spare = (hops[i].Dep - hops[i - 1].Arr) - rules.Required(hops[i - 1], h) + cutoff;
                explanations.Add(new Explanation("CHECKIN_CUTOFF_OK", P(("minutes", cutoff), ("spareMinutes", spare - cutoff), ("legIndex", i)), Severity.Good));
            }
        }

        if (hops.Any(h => h.Synthetic))
            explanations.Add(new Explanation("SYNTHETIC_TIMETABLE", P(), Severity.Caution));
        if (hops.Any(h => h.Assumed))
            explanations.Add(new Explanation("ASSUMED_LINK", P(), Severity.Info));
        if (hops.Any(h => h.Mode == TransportMode.Taxi && h.Pricing == PricingKind.Estimate))
            explanations.Add(new Explanation("ESTIMATED_TAXI_FARE", P(), Severity.Info));

        // Overnight: a long wait between scheduled legs that crosses local midnight.
        long? lastSchedArr = null;
        foreach (var h in hops)
        {
            if (!h.IsScheduled) continue;
            if (lastSchedArr is { } a)
            {
                var wait = h.Dep - a;
                if (wait >= ctx.Options.OvernightWaitMin && TimeMath.LocalDate(a) != TimeMath.LocalDate(h.Dep))
                    explanations.Add(new Explanation("OVERNIGHT_REQUIRED", P(("waitMinutes", wait)), Severity.Caution));
            }
            lastSchedArr = h.Arr;
        }

        // History, only when there is enough of it to say anything.
        double? worst = null, best = null;
        foreach (var h in hops.Where(x => x.IsScheduled))
        {
            var stat = ctx.Delays.StatFor(h.ServiceKey);
            if (stat is null || stat.Samples < ctx.Options.Delays.MinSamplesForExplanation) continue;
            worst = worst is null ? stat.OnTimeRate : Math.Min(worst.Value, stat.OnTimeRate);
            best = best is null ? stat.OnTimeRate : Math.Max(best.Value, stat.OnTimeRate);
        }
        if (worst is { } w && w <= ctx.Options.Delays.PoorOnTimeRate)
            explanations.Add(new Explanation("DELAY_HISTORY_POOR", P(("percent", (int)Math.Round(w * 100))), Severity.Caution));
        else if (worst is { } w2 && w2 >= ctx.Options.Delays.GoodOnTimeRate)
            explanations.Add(new Explanation("DELAY_HISTORY_GOOD", P(("percent", (int)Math.Round(w2 * 100))), Severity.Good));
    }

    public static string ModeCode(TransportMode mode) => mode switch
    {
        TransportMode.DomesticFlight => "domestic_flight",
        TransportMode.ResortTransfer => "resort_transfer",
        _ => mode.ToString().ToLowerInvariant(),
    };
}
