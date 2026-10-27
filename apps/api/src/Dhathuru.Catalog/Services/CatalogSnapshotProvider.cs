using Dhathuru.Catalog.Data;
using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Pricing;
using Dhathuru.Domain.Seams;
using Dhathuru.Domain.Transport;

namespace Dhathuru.Catalog.Services;

/// <summary>
/// Builds an immutable timetable snapshot for the trip engine. Pure in-memory work over the cached catalog,
/// so a week of every service builds in milliseconds. Synthetic data stays flagged on every run.
/// </summary>
public sealed class CatalogSnapshotProvider(CatalogCache cache, TimeProvider time) : ITimetableSnapshotProvider
{
    /// <summary>Hour used to pick a taxi time-of-day band for transfers; the engine adds its own buffers.</summary>
    internal const int TaxiReferenceHour = 12;

    public async Task<TimetableSnapshot> GetSnapshotAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct)
    {
        if (toDate < fromDate) throw new ArgumentException("toDate must not be before fromDate.", nameof(toDate));
        var index = await cache.GetAsync(ct);
        return Build(index, fromDate, toDate, time.GetUtcNow());
    }

    public static TimetableSnapshot Build(CatalogIndex index, DateOnly fromDate, DateOnly toDate, DateTimeOffset generatedAt)
    {
        var data = index.Data;
        var places = data.Places
            .Select(p => new SnapshotPlace(p.Id, p.Type, p.Name["en"], p.Geo, p.ParentId, p.IsHub, p.Approximate))
            .ToList();

        var runs = new List<SnapshotRun>();
        for (var date = fromDate; date <= toDate; date = date.AddDays(1))
        {
            foreach (var s in data.Services)
            {
                if (s.OnDemand || s.From is null || s.To is null) continue;
                var providerId = index.OperatorsById[s.OperatorId].ProviderId;
                foreach (var run in ServiceCalendar.RunsFor(s, date))
                {
                    runs.Add(new SnapshotRun(
                        run.ServiceRunId, s.Id, providerId, s.Mode, s.From.Value, s.To.Value, run.DepartAt, run.ArriveAt,
                        s.Fare, s.Pricing, s.CheckInCutoffMin, s.Synthetic));
                }
            }
        }
        // Total order, so the same catalog always gives byte-identical snapshots regardless of source row order.
        runs.Sort((a, b) => a.DepartAt != b.DepartAt ? a.DepartAt.CompareTo(b.DepartAt) : string.CompareOrdinal(a.ServiceRunId, b.ServiceRunId));

        var transfers = data.Links
            .Select(l => new SnapshotTransfer(l.From, l.To, l.Mode, l.DurationMin, l.Fare, l.Pricing))
            .ToList();
        transfers.AddRange(TaxiTransfers(index));

        return new TimetableSnapshot($"{data.Version}:{fromDate:yyyyMMdd}-{toDate:yyyyMMdd}", generatedAt, places, runs, transfers);
    }

    /// <summary>Taxi transfers between every ordered pair of taxi zones. Fixed when the operator's matrix has the fare, otherwise an estimate.</summary>
    internal static IEnumerable<SnapshotTransfer> TaxiTransfers(CatalogIndex index)
    {
        var taxi = index.Data.Taxi;
        if (taxi is null) yield break;
        var zones = index.Data.Places.Where(p => p.ZoneKey is not null).ToList();
        foreach (var a in zones)
        {
            foreach (var b in zones)
            {
                if (a.Id == b.Id) continue;
                var km = GeoMath.HaversineKm(a.Geo, b.Geo);
                var est = taxi.Estimate(a.ZoneKey, b.ZoneKey, km, TaxiReferenceHour);
                var pricing = est.Basis == EstimateBasis.Fallback ? PricingKind.Estimate : PricingKind.Fixed;
                yield return new SnapshotTransfer(a.Id, b.Id, TransportMode.Taxi, Math.Max(1, est.DurationMin), est.Fare, pricing);
            }
        }
    }
}
