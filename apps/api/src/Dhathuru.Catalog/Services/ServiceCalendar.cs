using System.Globalization;
using Dhathuru.Catalog.Data;
using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Time;

namespace Dhathuru.Catalog.Services;

/// <summary>One concrete departure of a service on a given Maldives local date.</summary>
public sealed record ScheduledRun(string ServiceRunId, DateTimeOffset DepartAt, DateTimeOffset ArriveAt);

public static class ServiceCalendar
{
    private const string RunTimeFormat = "yyyy-MM-dd'T'HH:mm'Z'";

    public static bool RunsOn(CatalogService s, DateOnly localDate)
    {
        if (s.OnDemand) return true;
        if (localDate < s.ValidFrom || (s.ValidTo is { } to && localDate > to)) return false;
        var iso = localDate.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)localDate.DayOfWeek;
        return s.DaysOfWeek.Contains(iso);
    }

    /// <summary>All local departure times of day, ascending, without duplicates.</summary>
    public static IReadOnlyList<TimeOnly> LocalTimes(CatalogService s)
    {
        var times = new SortedSet<TimeOnly>(s.Times);
        if (s.Frequency is { } f && f.HeadwayMin > 0)
        {
            for (var t = f.First; t <= f.Last; t = t.AddMinutes(f.HeadwayMin))
            {
                times.Add(t);
                if (t.AddMinutes(f.HeadwayMin) < t) break; // wrapped past midnight
            }
        }
        return [.. times];
    }

    public static IEnumerable<ScheduledRun> RunsFor(CatalogService s, DateOnly localDate)
    {
        if (s.OnDemand || !RunsOn(s, localDate)) yield break;
        foreach (var t in LocalTimes(s))
        {
            var depart = MaldivesTime.FromLocal(localDate, t);
            yield return new ScheduledRun(RunId(s.Id, depart), depart, depart.AddMinutes(s.DurationMin));
        }
    }

    public static string RunId(ServiceId id, DateTimeOffset departUtc) =>
        $"{id}:{departUtc.UtcDateTime.ToString(RunTimeFormat, CultureInfo.InvariantCulture)}";

    public static bool TryParseRunId(string? runId, out ServiceId serviceId, out DateTimeOffset departAt)
    {
        serviceId = default;
        departAt = default;
        if (runId is null) return false;
        var split = runId.IndexOf(':', StringComparison.Ordinal);
        if (split < 0) return false;
        return ServiceId.TryParse(runId[..split], out serviceId)
               && DateTimeOffset.TryParseExact(runId[(split + 1)..], RunTimeFormat, CultureInfo.InvariantCulture,
                   DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out departAt);
    }
}
