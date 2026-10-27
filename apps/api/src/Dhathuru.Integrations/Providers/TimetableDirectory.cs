using System.Globalization;
using Dhathuru.Catalog.Data;
using Dhathuru.Catalog.Services;
using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Pricing;
using Dhathuru.Domain.Providers;
using Dhathuru.Domain.Time;
using Dhathuru.Domain.Transport;

namespace Dhathuru.Integrations.Providers;

/// <summary>A run found by id: what it is, when it leaves and what one passenger pays.</summary>
public sealed record ResolvedRun(
    string ServiceRunId,
    CatalogService Service,
    PlaceId From,
    PlaceId To,
    DateTimeOffset DepartAt,
    DateTimeOffset ArriveAt,
    Money FarePerPassenger,
    PricingKind Pricing);

/// <summary>
/// Turns the catalog into departures and resolves run ids. Shared by the static and mock providers, so a mock and the
/// timetable it simulates can never disagree about what runs when.
/// </summary>
public sealed class TimetableDirectory(CatalogCache cache, TimeProvider time)
{
    internal const int TaxiGranularityMin = 5;

    /// <summary>A taxi cannot leave this instant: the earliest on-demand pickup is this many minutes from now.</summary>
    internal const int TaxiLeadMin = 5;

    /// <summary>On-demand run ids carry the place pair: <c>svc_...:2026-10-27T05:00Z@from&gt;to</c>.</summary>
    public static string TaxiRunId(ServiceId service, PlaceId from, PlaceId to, DateTimeOffset departAt) =>
        $"{ServiceCalendar.RunId(service, departAt)}@{from}>{to}";

    public async Task<IReadOnlyList<ServiceDeparture>> DeparturesAsync(string providerId, DepartureQuery q, CancellationToken ct)
    {
        var index = await cache.GetAsync(ct);
        var result = new List<ServiceDeparture>();
        var firstDay = MaldivesTime.LocalDate(q.WindowStart);
        var lastDay = MaldivesTime.LocalDate(q.WindowEnd.AddTicks(-1));

        foreach (var s in index.Data.Services)
        {
            if (index.OperatorsById[s.OperatorId].ProviderId != providerId) continue;
            if (s.OnDemand)
            {
                if (TaxiDeparture(index, s, q) is { } taxi) result.Add(taxi);
                continue;
            }
            if (s.From != q.From || s.To != q.To) continue;
            for (var day = firstDay; day <= lastDay; day = day.AddDays(1))
            {
                foreach (var run in ServiceCalendar.RunsFor(s, day))
                {
                    if (run.DepartAt < q.WindowStart || run.DepartAt >= q.WindowEnd) continue;
                    result.Add(new ServiceDeparture(run.ServiceRunId, s.Id, q.From, q.To, run.DepartAt, run.ArriveAt, s.Fare, s.Pricing));
                }
            }
        }
        result.Sort((a, b) => a.DepartAt.CompareTo(b.DepartAt));
        return result;
    }

    public async Task<ResolvedRun?> ResolveAsync(string providerId, string serviceRunId, CancellationToken ct)
    {
        var index = await cache.GetAsync(ct);
        var at = serviceRunId.IndexOf('@', StringComparison.Ordinal);
        var basePart = at < 0 ? serviceRunId : serviceRunId[..at];
        if (!ServiceCalendar.TryParseRunId(basePart, out var serviceId, out var departAt)) return null;
        if (!index.ServicesById.TryGetValue(serviceId, out var service)) return null;
        if (index.OperatorsById[service.OperatorId].ProviderId != providerId) return null;

        if (service.OnDemand)
        {
            if (at < 0) return null;
            var pair = serviceRunId[(at + 1)..].Split('>');
            if (pair.Length != 2 || !PlaceId.TryParse(pair[0], out var from) || !PlaceId.TryParse(pair[1], out var to)) return null;
            if (!index.PlacesById.TryGetValue(from, out var a) || !index.PlacesById.TryGetValue(to, out var b)) return null;
            var estimate = EstimateTaxi(index, a, b, departAt);
            return estimate is null
                ? null
                : new ResolvedRun(serviceRunId, service, from, to, departAt, departAt.AddMinutes(estimate.DurationMin), estimate.Fare,
                    estimate.Basis == EstimateBasis.Fallback ? PricingKind.Estimate : PricingKind.Fixed);
        }

        if (at >= 0 || service.From is null || service.To is null) return null;
        var localDate = MaldivesTime.LocalDate(departAt);
        var run = ServiceCalendar.RunsFor(service, localDate).FirstOrDefault(r => r.DepartAt == departAt);
        return run is null
            ? null
            : new ResolvedRun(serviceRunId, service, service.From.Value, service.To.Value, run.DepartAt, run.ArriveAt, service.Fare, service.Pricing);
    }

    private ServiceDeparture? TaxiDeparture(CatalogIndex index, CatalogService s, DepartureQuery q)
    {
        if (!index.PlacesById.TryGetValue(q.From, out var a) || !index.PlacesById.TryGetValue(q.To, out var b) || q.From == q.To) return null;
        var readyAt = time.GetUtcNow().AddMinutes(TaxiLeadMin);
        var earliest = RoundUp(q.WindowStart < readyAt ? readyAt : q.WindowStart);
        if (earliest >= q.WindowEnd) return null;
        var estimate = EstimateTaxi(index, a, b, earliest);
        if (estimate is null) return null;
        return new ServiceDeparture(
            TaxiRunId(s.Id, q.From, q.To, earliest), s.Id, q.From, q.To, earliest, earliest.AddMinutes(estimate.DurationMin), estimate.Fare,
            estimate.Basis == EstimateBasis.Fallback ? PricingKind.Estimate : PricingKind.Fixed);
    }

    private static TaxiEstimate? EstimateTaxi(CatalogIndex index, CatalogPlace a, CatalogPlace b, DateTimeOffset departAt)
    {
        if (index.Data.Taxi is not { } taxi) return null;
        var hour = MaldivesTime.ToLocal(departAt).Hour;
        return taxi.Estimate(a.ZoneKey, b.ZoneKey, GeoMath.HaversineKm(a.Geo, b.Geo), hour);
    }

    private static DateTimeOffset RoundUp(DateTimeOffset t)
    {
        var ticks = TimeSpan.FromMinutes(TaxiGranularityMin).Ticks;
        var rounded = (t.UtcTicks + ticks - 1) / ticks * ticks;
        return new DateTimeOffset(rounded, TimeSpan.Zero);
    }

    internal static string Describe(DateTimeOffset t) => t.ToString("O", CultureInfo.InvariantCulture);
}
