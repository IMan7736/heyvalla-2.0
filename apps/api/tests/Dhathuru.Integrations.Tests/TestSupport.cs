using Dhathuru.Catalog.Data;
using Dhathuru.Catalog.Services;
using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Providers;
using Dhathuru.Domain.Time;
using Dhathuru.Integrations.Providers;

namespace Dhathuru.Integrations.Tests;

internal sealed class TestClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset now = start;

    public override DateTimeOffset GetUtcNow() => now;

    public void Advance(TimeSpan by) => now += by;
}

/// <summary>A catalog, a clock and the shared pieces every provider test needs.</summary>
internal sealed class World
{
    /// <summary>Tuesday 2026-10-27, 10:00 Maldives time (05:00 UTC).</summary>
    public static readonly DateTimeOffset Start = new(2026, 10, 27, 5, 0, 0, TimeSpan.Zero);

    public World(DateTimeOffset? start = null)
    {
        Data = LoadSeed();
        Clock = new TestClock(start ?? Start);
        Catalog = new CatalogCache(new StaticCatalogDataSource(Data), Clock);
        Directory = new TimetableDirectory(Catalog, Clock);
        Board = new DisruptionBoard(Clock);
    }

    public CatalogData Data { get; }

    public TestClock Clock { get; }

    public CatalogCache Catalog { get; }

    public TimetableDirectory Directory { get; }

    public DisruptionBoard Board { get; }

    public CatalogService Service(string slug) => Data.Services.Single(s => s.Slug == slug);

    public string ProviderOf(CatalogService s) => Data.Operators.Single(o => o.Id == s.OperatorId).ProviderId;

    public MockTransportProvider Mock(string providerId) => new(providerId, Directory, Board, Clock);

    public StaticTimetableProvider Static(string providerId) => new(providerId, Directory, Clock);

    /// <summary>First future departure of a service on the given local day.</summary>
    public async Task<ServiceDeparture> NextDeparture(ITransportProvider provider, string slug, DateOnly? day = null)
    {
        var s = Service(slug);
        var (start, end) = MaldivesTime.DayBounds(day ?? MaldivesTime.LocalDate(Clock.GetUtcNow()));
        var all = await provider.GetDeparturesAsync(new DepartureQuery(s.From!.Value, s.To!.Value, start, end), TestContext.Current.CancellationToken);
        return all.First(d => d.DepartAt > Clock.GetUtcNow());
    }

    private static CatalogData LoadSeed()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var nested = Path.Combine(dir.FullName, "apps", "api", "seed");
            if (File.Exists(Path.Combine(nested, SeedFiles.CatalogFile))) return SeedFiles.Load(nested);
            var direct = Path.Combine(dir.FullName, "seed");
            if (File.Exists(Path.Combine(direct, SeedFiles.CatalogFile))) return SeedFiles.Load(direct);
        }
        throw new DirectoryNotFoundException("apps/api/seed not found");
    }
}
