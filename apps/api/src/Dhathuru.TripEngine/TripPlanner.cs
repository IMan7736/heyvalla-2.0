using System.Collections.Concurrent;
using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Places;
using Dhathuru.Domain.Seams;
using Dhathuru.Domain.Time;
using Dhathuru.Domain.Transport;
using Dhathuru.Domain.Trips;
using Dhathuru.TripEngine.Assembly;
using Dhathuru.TripEngine.Configuration;
using Dhathuru.TripEngine.Network;
using Dhathuru.TripEngine.Ranking;
using Dhathuru.TripEngine.Delays;
using Dhathuru.TripEngine.Search;
using Dhathuru.TripEngine.Support;

namespace Dhathuru.TripEngine;

/// <summary>A verified way out of an empty result. Codes: TRY_LATER, TRY_EARLIER, TRY_NEXT_DAY, ALLOW_MORE_TRANSFERS, REMOVE_BUDGET, ALLOW_ALL_MODES.</summary>
public sealed record Suggestion(string Code, IReadOnlyDictionary<string, object?> Params);

/// <summary>A search result plus structured notices (codes only) that explain gaps: unknown place, no route, closed taxis, and so on.</summary>
public sealed record PlanResult(TripSearchResult Result, IReadOnlyList<Explanation> Notices, IReadOnlyList<Suggestion>? Suggestions = null)
{
    /// <summary>What to change to get a result when there are no options. Each one was checked: applying it yields at least one option. Never null.</summary>
    public IReadOnlyList<Suggestion> SuggestionList => Suggestions ?? [];

    public IReadOnlyList<TripOption> Options => Result.Options;
}

/// <summary>
/// The trip engine entry point. Pure orchestration over an in-memory snapshot: no database, no HTTP.
/// Same query + same snapshot + same reliability data = same options, ids included.
/// </summary>
public sealed partial class TripPlanner : ITripPlanner
{
    private readonly ITimetableSnapshotProvider snapshots;
    private readonly IReliabilityStats reliability;
    private readonly IBookedItineraryProvider? bookings;
    private readonly EngineOptions options;
    private readonly IOptionStore store;
    private readonly TimeProvider clock;
    private readonly ConcurrentDictionary<string, (ReliabilityStat? Stat, DateTimeOffset At)> statCache = new();
    private readonly object networkGate = new();
    private (string Key, TimetableNetwork Network)? lastNetwork;

    public TripPlanner(
        ITimetableSnapshotProvider snapshots,
        IReliabilityStats reliability,
        IBookedItineraryProvider? bookings = null,
        EngineOptions? options = null,
        IOptionStore? store = null,
        TimeProvider? clock = null)
    {
        this.snapshots = snapshots;
        this.reliability = reliability;
        this.bookings = bookings;
        this.options = options ?? new EngineOptions();
        this.clock = clock ?? TimeProvider.System;
        this.store = store ?? new InMemoryOptionStore(this.clock);
    }

    public async Task<TripSearchResult> SearchAsync(TripSearchQuery query, CancellationToken ct) =>
        (await PlanAsync(query, ct).ConfigureAwait(false)).Result;

    public Task<TripOption?> GetOptionAsync(TripOptionId id, CancellationToken ct) =>
        Task.FromResult(store.Get(id));

    public async Task<TripSearchResult> ReplanAsync(ReplanQuery query, CancellationToken ct) =>
        (await PlanReplanAsync(query, ct).ConfigureAwait(false)).Result;

    // ---- search ----------------------------------------------------------------------------------------------------

    public async Task<PlanResult> PlanAsync(TripSearchQuery q, CancellationToken ct)
    {
        var notices = new List<Explanation>();
        var searchId = StableIds.Search(QuerySignature(q));

        if (q.Passengers.Adults < 1)
            return Empty(searchId, notices, "PASSENGERS_INVALID");
        if (q.From == q.To)
            return Empty(searchId, notices, "ORIGIN_EQUALS_DESTINATION");

        var snapshot = await snapshots.GetSnapshotAsync(
            q.Date.AddDays(-options.SnapshotDaysBefore), q.Date.AddDays(options.SnapshotDaysAfter), ct).ConfigureAwait(false);
        var network = NetworkFor(snapshot);

        var unknown = false;
        if (!network.HasPlace(q.From)) { notices.Add(Notice("PLACE_UNKNOWN", Severity.Caution, ("role", "origin"))); unknown = true; }
        if (!network.HasPlace(q.To)) { notices.Add(Notice("PLACE_UNKNOWN", Severity.Caution, ("role", "destination"))); unknown = true; }
        if (unknown) return new PlanResult(new TripSearchResult(searchId, []), notices);

        var now = TimeMath.Minute(clock.GetUtcNow());
        var anchor = TimeMath.Minute(MaldivesTime.FromLocal(q.Date, q.Time));
        var backward = q.TimeMode == TimeMode.ArriveBy;
        // Nothing that has already left (or whose check-in has closed) is ever returned, in either direction.
        long? notBefore = now;
        if (!backward && anchor < now)
        {
            anchor = now;
            notices.Add(Notice("DEPARTURE_IN_PAST_ADJUSTED", Severity.Info));
        }
        if (backward && anchor <= now)
            return Empty(searchId, notices, "DEADLINE_IN_PAST");

        var delays = new DelayModel(options.Delays, await LoadStatsAsync(network, notices, ct).ConfigureAwait(false));
        var router = new Router(network, options, delays);
        var modes = q.Preferences.Modes is { Count: > 0 } m ? m.ToHashSet() : null;
        var request = new RouteRequest
        {
            Origin = q.From,
            Goal = q.To,
            AnchorMinute = anchor,
            Backward = backward,
            Passengers = q.Passengers,
            MaxTransfers = q.Preferences.MaxTransfers,
            BudgetMinor = q.Preferences.BudgetMax?.AmountMinor,
            Modes = modes,
        };

        var routed = router.Search(request);
        var ctx = new AssemblyContext(options, network, delays, q.Passengers, q.Preferences.RiskTolerance,
            snapshot.Version, backward ? null : anchor, backward ? anchor : null, notBefore);
        var assembler = new ItineraryAssembler(ctx);
        var candidates = AssembleAll(routed, assembler);
        AddAlternatives(router, request, assembler, candidates, ct);

        var ranked = new Ranker(options).Rank(candidates, q.Preferences.RiskTolerance, backward, anchor, snapshot.Version, QuerySignature(q));
        if (routed.Truncated) notices.Add(Notice("SEARCH_TRUNCATED", Severity.Caution));
        if (ranked.ToleranceRelaxed)
            notices.Add(Notice("NO_OPTION_WITHIN_RISK_TOLERANCE", Severity.Caution, ("tolerance", q.Preferences.RiskTolerance.ToString().ToLowerInvariant())));

        foreach (var o in ranked.Options) store.Put(o);
        var suggestions = new List<Suggestion>();
        if (ranked.Options.Count == 0)
            Diagnose(q, network, delays, request, routed, anchor, backward, now, notices, suggestions);

        return new PlanResult(new TripSearchResult(searchId, ranked.Options), notices, suggestions);
    }

    private static List<Candidate> AssembleAll(RouteResult routed, ItineraryAssembler assembler)
    {
        var list = new List<Candidate>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rc in routed.Candidates)
        {
            var c = assembler.Assemble(rc.Hops);
            if (c is not null && seen.Add(c.Signature)) list.Add(c);
        }
        return list;
    }

    /// <summary>
    /// The router keeps only non-dominated paths, so a later flight that is merely "the same but later" never survives it.
    /// Travellers want that backup. Ban each scheduled run of the best few options in turn and search again: the next-best
    /// way around it. Bounded (a handful of extra searches) and skipped when the first pass already filled the list.
    /// </summary>
    private void AddAlternatives(Router router, RouteRequest request, ItineraryAssembler assembler, List<Candidate> candidates, CancellationToken ct)
    {
        if (candidates.Count == 0 || candidates.Count >= options.MaxOptions) return;
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var banned = new HashSet<string>(StringComparer.Ordinal);
        var seen = candidates.Select(c => c.Signature).ToHashSet(StringComparer.Ordinal);
        var seeds = candidates.OrderBy(c => c.ElapsedKey(request.Backward)).Take(3).ToList();
        foreach (var seed in seeds)
        {
            foreach (var hop in seed.Hops.Where(h => h.IsScheduled))
            {
                if (clock.ElapsedMilliseconds > options.AlternativeSearchBudgetMs || ct.IsCancellationRequested) return;
                if (!banned.Add(hop.Key)) continue;
                var again = router.Search(request with { ExcludeRuns = new HashSet<string> { hop.Key } });
                foreach (var c in AssembleAll(again, assembler))
                {
                    if (seen.Add(c.Signature)) candidates.Add(c);
                }
            }
        }
    }

    // ---- why there is nothing ------------------------------------------------------------------------------------------

    private void Diagnose(
        TripSearchQuery q,
        TimetableNetwork network,
        DelayModel delays,
        RouteRequest request,
        RouteResult routed,
        long anchor,
        bool backward,
        long now,
        List<Explanation> notices,
        List<Suggestion> suggestions)
    {
        if (!network.IsConnected(q.From, q.To))
        {
            notices.Add(Notice("NO_ROUTE_EXISTS", Severity.Caution));
            if (network.DeparturesFrom(q.From).Length == 0 && network.LinksFrom(q.From).Length == 0)
                notices.Add(Notice("NO_SERVICES_AT_ORIGIN", Severity.Info));
            if (network.ArrivalsAt(q.To).Length == 0 && network.LinksTo(q.To).Length == 0)
                notices.Add(Notice("NO_SERVICES_AT_DESTINATION", Severity.Info));
            return;
        }

        var router = new Router(network, options, delays);
        var assembler = new ItineraryAssembler(new AssemblyContext(options, network, delays, q.Passengers, q.Preferences.RiskTolerance,
            network.Version, backward ? null : anchor, backward ? anchor : null));

        // Which user-set constraint is the cause? Re-run without it.
        if (request.Modes is not null && Works(request with { Modes = null }, out _))
        {
            notices.Add(Notice("MODE_FILTER_EXCLUDES_ALL", Severity.Caution));
            notices.Add(Notice("SUGGEST_ALLOW_MORE_MODES", Severity.Info));
            suggestions.Add(Suggest("ALLOW_ALL_MODES"));
            return;
        }
        if (request.BudgetMinor is not null && Works(request with { BudgetMinor = null }, out var cheapest))
        {
            notices.Add(Notice("BUDGET_EXCEEDED", Severity.Caution));
            notices.Add(Notice("SUGGEST_INCREASE_BUDGET", Severity.Info, ("minimumAmountMinor", (object?)cheapest)));
            suggestions.Add(Suggest("REMOVE_BUDGET", ("minimumAmountMinor", cheapest)));
            return;
        }
        if (request.MaxTransfers is not null && Works(request with { MaxTransfers = null }, out _))
        {
            notices.Add(Notice("MAX_TRANSFERS_EXCEEDED", Severity.Caution));
            notices.Add(Notice("SUGGEST_MORE_TRANSFERS", Severity.Info));
            suggestions.Add(Suggest("ALLOW_MORE_TRANSFERS"));
            return;
        }

        if (routed.RejectedTaxiClosed > 0) notices.Add(Notice("TAXI_UNAVAILABLE_AT_NIGHT", Severity.Caution));
        if (routed.RejectedTooTight > 0) notices.Add(Notice("CONNECTION_TOO_TIGHT", Severity.Caution));
        notices.Add(Notice("NO_SAFE_CONNECTION", Severity.Caution));

        if (backward)
        {
            // Arrive-by could not be met. When would the earliest safe arrival be?
            var dayStart = TimeMath.Minute(MaldivesTime.FromLocal(q.Date, TimeOnly.MinValue));
            var forward = router.Search(request with { Backward = false, AnchorMinute = dayStart, MaxTransfers = null, BudgetMinor = null });
            var fwdAssembler = new ItineraryAssembler(new AssemblyContext(options, network, delays, q.Passengers, q.Preferences.RiskTolerance,
                network.Version, dayStart, null));
            var earliest = AssembleAll(forward, fwdAssembler).MinBy(c => c.ArriveMin);
            if (earliest is not null)
            {
                notices.Add(Notice("EARLIEST_POSSIBLE_ARRIVAL", Severity.Info, ("arriveAt", TimeMath.Instant(earliest.ArriveMin).ToString("o"))));
                notices.Add(Notice("SUGGEST_LATER_ARRIVAL", Severity.Info));
                suggestions.Add(Suggest("TRY_LATER", ("arriveAt", TimeMath.Instant(earliest.ArriveMin).ToString("o"))));
                return;
            }
        }
        else
        {
            notices.Add(Notice("NO_SERVICE_WITHIN_HORIZON", Severity.Info));
            // Forward search: probe the next local day, and the start of today if the requested time is later than that.
            var nextDay = TimeMath.Minute(MaldivesTime.FromLocal(q.Date.AddDays(1), TimeOnly.MinValue));
            if (FirstDeparture(nextDay) is { } nd)
                suggestions.Add(Suggest("TRY_NEXT_DAY", ("departAt", TimeMath.Instant(nd).ToString("o"))));
            var todayStart = Math.Max(now, TimeMath.Minute(MaldivesTime.FromLocal(q.Date, TimeOnly.MinValue)));
            if (todayStart < anchor && FirstDeparture(todayStart) is { } ed && ed < anchor)
                suggestions.Add(Suggest("TRY_EARLIER", ("departAt", TimeMath.Instant(ed).ToString("o"))));
        }
        notices.Add(Notice("SUGGEST_TRY_OTHER_DATE", Severity.Info));

        long? FirstDeparture(long from)
        {
            var res = router.Search(request with { Backward = false, AnchorMinute = from, MaxTransfers = null, BudgetMinor = null });
            var asm = new ItineraryAssembler(new AssemblyContext(options, network, delays, q.Passengers, q.Preferences.RiskTolerance,
                network.Version, from, null, Math.Max(now, from)));
            var first = AssembleAll(res, asm).MinBy(c => c.DepartMin);
            return first?.DepartMin;
        }

        bool Works(RouteRequest r, out long cheapestMinor)
        {
            var res = router.Search(r);
            var cands = AssembleAll(res, assembler);
            cheapestMinor = cands.Count == 0 ? 0 : cands.Min(c => c.TotalMinor);
            return cands.Count > 0;
        }
    }

    // ---- shared plumbing -----------------------------------------------------------------------------------------------

    internal TimetableNetwork NetworkFor(TimetableSnapshot snapshot)
    {
        var key = $"{snapshot.Version}|{snapshot.Runs.Count}|{snapshot.Places.Count}|{snapshot.Transfers.Count}";
        lock (networkGate)
        {
            if (lastNetwork is { } hit && hit.Key == key) return hit.Network;
        }
        var built = new TimetableNetwork(snapshot, options);
        lock (networkGate) lastNetwork = (key, built);
        return built;
    }

    internal async Task<IReadOnlyDictionary<string, ReliabilityStat?>> LoadStatsAsync(
        TimetableNetwork network, List<Explanation> notices, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var ttl = TimeSpan.FromMinutes(10);
        var keys = network.Runs.Where(r => r.Run is not null).Select(r => r.Run!.ServiceId).Distinct().OrderBy(s => s.Value, StringComparer.Ordinal).ToList();
        var result = new Dictionary<string, ReliabilityStat?>();
        var failed = 0;
        using var gate = new SemaphoreSlim(8);
        await Task.WhenAll(keys.Select(async id =>
        {
            if (statCache.TryGetValue(id.Value, out var hit) && now - hit.At < ttl)
            {
                lock (result) result[id.Value] = hit.Stat;
                return;
            }
            await gate.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var stat = await reliability.GetAsync(id, ct).ConfigureAwait(false);
                statCache[id.Value] = (stat, now);
                lock (result) result[id.Value] = stat;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                // Unknown history is treated as "no history", never as "perfect".
                Interlocked.Increment(ref failed);
                lock (result) result[id.Value] = null;
            }
            finally { gate.Release(); }
        })).ConfigureAwait(false);
        if (failed > 0) notices.Add(Notice("RELIABILITY_UNAVAILABLE", Severity.Info, ("services", failed)));
        return result;
    }

    internal static string QuerySignature(TripSearchQuery q)
    {
        var p = q.Preferences;
        return string.Join('|', q.From, q.To, q.Date.ToString("yyyy-MM-dd"), q.Time.ToString("HH:mm"), q.TimeMode,
            q.Passengers.Adults, q.Passengers.Children, q.Passengers.Infants, p.RiskTolerance, p.MaxTransfers,
            p.BudgetMax?.AmountMinor, p.Modes is null ? "" : string.Join(',', p.Modes.Order()));
    }

    private static Suggestion Suggest(string code, params (string Key, object? Value)[] values) =>
        new(code, values.ToDictionary(v => v.Key, v => v.Value));

    internal static Explanation Notice(string code, Severity severity, params (string Key, object? Value)[] values) =>
        new(code, values.ToDictionary(v => v.Key, v => v.Value), severity);

    private static PlanResult Empty(SearchId id, List<Explanation> notices, string code)
    {
        notices.Add(Notice(code, Severity.Caution));
        return new PlanResult(new TripSearchResult(id, []), notices);
    }
}
