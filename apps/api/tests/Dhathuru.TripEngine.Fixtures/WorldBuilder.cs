using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Places;
using Dhathuru.Domain.Pricing;
using Dhathuru.Domain.Seams;
using Dhathuru.Domain.Time;
using Dhathuru.Domain.Transport;

namespace Dhathuru.TripEngine.Fixtures;

/// <summary>
/// Builds synthetic timetable snapshots for tests, benchmarks and the CLI. Everything here is invented: ids are counters,
/// names are made up or public place names, fares are round numbers. No dataset rows are used.
/// </summary>
public sealed class WorldBuilder
{
    private readonly List<SnapshotPlace> places = [];
    private readonly List<SnapshotRun> runs = [];
    private readonly List<SnapshotTransfer> transfers = [];
    private readonly Dictionary<string, ServiceId> services = [];
    private readonly HashSet<string> runIds = [];
    private readonly Dictionary<PlaceId, string> names = [];
    private int placeCounter;
    private int serviceCounter;

    public IReadOnlyDictionary<PlaceId, string> Names => names;

    public PlaceId Place(string name, PlaceType type, PlaceId? parent = null, double lat = 4.0, double lng = 73.5, bool hub = false)
    {
        var id = PlaceId.Parse($"{PlaceId.PrefixFor(type)}_{(++placeCounter).ToString("D26")}");
        places.Add(new SnapshotPlace(id, type, name, new GeoPoint(lat, lng), parent, hub, Approximate: false));
        names[id] = name;
        return id;
    }

    public ServiceId Service(string name)
    {
        if (!services.TryGetValue(name, out var id))
        {
            id = ServiceId.Parse($"svc_{(++serviceCounter).ToString("D26")}");
            services[name] = id;
        }
        return id;
    }

    /// <summary>Add one departure. <paramref name="depLocal"/> is "yyyy-MM-dd HH:mm" in Maldives time.</summary>
    public WorldBuilder Run(
        string service,
        TransportMode mode,
        PlaceId from,
        PlaceId to,
        string depLocal,
        int minutes,
        long fareMvr,
        int checkInMin = 0,
        bool synthetic = false,
        string? provider = null)
    {
        var dep = Local(depLocal);
        var runId = $"{service}|{depLocal}|{from}";
        if (!runIds.Add(runId)) return this; // the same departure twice is the same departure
        runs.Add(new SnapshotRun(
            runId,
            Service(service),
            provider ?? $"prov-{service}",
            mode,
            from,
            to,
            dep,
            dep.AddMinutes(minutes),
            Money.FromMvr(fareMvr),
            PricingKind.Fixed,
            checkInMin,
            synthetic));
        return this;
    }

    /// <summary>A service that runs every <paramref name="everyMin"/> minutes from <paramref name="firstHhmm"/>, <paramref name="count"/> times, on each given date.</summary>
    public WorldBuilder Daily(
        string service,
        TransportMode mode,
        PlaceId from,
        PlaceId to,
        IEnumerable<string> dates,
        string firstHhmm,
        int everyMin,
        int count,
        int minutes,
        long fareMvr,
        int checkInMin = 0)
    {
        foreach (var date in dates)
        {
            var first = TimeOnly.Parse(firstHhmm);
            for (var i = 0; i < count; i++)
            {
                var t = first.AddMinutes(i * everyMin);
                var day = DateOnly.Parse(date).AddDays((first.ToTimeSpan() + TimeSpan.FromMinutes(i * everyMin)).Days);
                Run(service, mode, from, to, $"{day:yyyy-MM-dd} {t:HH:mm}", minutes, fareMvr, checkInMin);
            }
        }
        return this;
    }

    public WorldBuilder Link(PlaceId from, PlaceId to, TransportMode mode, int minutes, long fareMvr, bool bothWays = true)
    {
        transfers.Add(new SnapshotTransfer(from, to, mode, minutes, Money.FromMvr(fareMvr), mode == TransportMode.Walk ? PricingKind.Fixed : PricingKind.Estimate));
        if (bothWays)
            transfers.Add(new SnapshotTransfer(to, from, mode, minutes, Money.FromMvr(fareMvr), mode == TransportMode.Walk ? PricingKind.Fixed : PricingKind.Estimate));
        return this;
    }

    public TimetableSnapshot Build(string version = "fixture-1") =>
        new(version, new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero), places.ToList(), runs.ToList(), transfers.ToList());

    public static DateTimeOffset Local(string yyyyMmddHhmm)
    {
        var parts = yyyyMmddHhmm.Split(' ');
        return MaldivesTime.FromLocal(DateOnly.Parse(parts[0]), TimeOnly.Parse(parts[1]));
    }
}

/// <summary>Serves a fixed snapshot, cut to the requested dates, and fixed reliability numbers.</summary>
public sealed class StaticSnapshotProvider(TimetableSnapshot snapshot) : ITimetableSnapshotProvider
{
    public int Calls { get; private set; }

    public Task<TimetableSnapshot> GetSnapshotAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct)
    {
        Calls++;
        var (start, _) = MaldivesTime.DayBounds(fromDate);
        var (_, end) = MaldivesTime.DayBounds(toDate);
        return Task.FromResult(snapshot with { Runs = snapshot.Runs.Where(r => r.DepartAt >= start && r.DepartAt < end).ToList() });
    }
}

public sealed class StaticReliability(IDictionary<ServiceId, ReliabilityStat>? stats = null) : IReliabilityStats
{
    public Task<ReliabilityStat?> GetAsync(ServiceId serviceId, CancellationToken ct) =>
        Task.FromResult(stats is not null && stats.TryGetValue(serviceId, out var s) ? s : null);
}
