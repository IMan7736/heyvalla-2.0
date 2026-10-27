using Dhathuru.Catalog.Data;
using Dhathuru.Catalog.Services;
using Dhathuru.Domain.Places;
using Dhathuru.Domain.Pricing;
using Dhathuru.Domain.Seams;
using Dhathuru.Domain.Transport;

namespace Dhathuru.Catalog.Tests;

public class TaxiModelTests
{
    private static readonly CatalogData Data = TestSupport.LoadSeed();
    private static readonly TaxiModel Taxi = Data.Taxi!;

    [Fact]
    public void Known_zone_pair_uses_the_operator_fare_and_observed_duration()
    {
        var e = Taxi.Estimate("male", "hulhumale-1", 8, 12);
        Assert.Equal(Money.FromMvr(85), e.Fare);
        Assert.Equal(EstimateBasis.Observed, e.Basis);
        Assert.InRange(e.DurationMin, 10, 16);
        Assert.True(e.DurationP90Min >= e.DurationMin);
        Assert.Equal(EstimateConfidence.High, e.Confidence);
    }

    [Fact]
    public void Same_zone_costs_the_minimum_fare()
    {
        Assert.Equal(Money.FromMvr(30), Taxi.Estimate("male", "male", 0.5, 12).Fare);
    }

    [Fact]
    public void Fare_matrix_is_symmetric_in_estimates()
    {
        foreach (var a in Taxi.Fares.Select(f => f.FromZone).Distinct())
            foreach (var b in Taxi.Fares.Select(f => f.ToZone).Distinct())
                Assert.Equal(Taxi.Estimate(a, b, 5, 12).Fare, Taxi.Estimate(b, a, 5, 12).Fare);
    }

    [Fact]
    public void Unknown_zone_falls_back_to_distance_and_is_labelled_a_guess()
    {
        var e = Taxi.Estimate(null, null, 10, 12);
        Assert.Equal(EstimateBasis.Fallback, e.Basis);
        Assert.Equal(EstimateConfidence.Low, e.Confidence);
        Assert.True(e.Fare.AmountMinor > 0);
        Assert.True(e.DurationMin > Taxi.Estimate(null, null, 2, 12).DurationMin);
    }

    [Fact]
    public void Fallback_duration_is_a_good_fit_but_fallback_fare_admits_it_is_not()
    {
        Assert.True(Taxi.DurationFallbackMin.R2 > 0.9);
        Assert.True(Taxi.FareFallbackMvr.R2 < 0.6, "fares are zone priced; the distance fit must not claim to be good");
    }

    [Theory]
    [InlineData(2, "night")]
    [InlineData(8, "morning")]
    [InlineData(13, "midday")]
    [InlineData(20, "evening")]
    public void Hours_map_to_time_bands(int hour, string band) => Assert.Equal(band, Taxi.BandFor(hour));
}

public class SnapshotTests
{
    private static readonly CatalogData Data = TestSupport.LoadSeed();
    private static readonly DateTimeOffset Now = new(2026, 10, 26, 0, 0, 0, TimeSpan.Zero);

    private static TimetableSnapshot Build(DateOnly from, DateOnly to) => CatalogSnapshotProvider.Build(new CatalogIndex(Data), from, to, Now);

    [Fact]
    public void One_day_contains_runs_on_that_local_day_only()
    {
        var day = new DateOnly(2026, 10, 27);
        var snap = Build(day, day);
        Assert.NotEmpty(snap.Runs);
        Assert.All(snap.Runs, r => Assert.Equal(day, Domain.Time.MaldivesTime.LocalDate(r.DepartAt)));
    }

    [Fact]
    public void Runs_are_sorted_and_arrive_after_they_depart()
    {
        var snap = Build(new DateOnly(2026, 10, 27), new DateOnly(2026, 10, 29));
        Assert.Equal(snap.Runs.OrderBy(r => r.DepartAt).ThenBy(r => r.ServiceRunId, StringComparer.Ordinal).Select(r => r.ServiceRunId), snap.Runs.Select(r => r.ServiceRunId));
        Assert.All(snap.Runs, r => Assert.True(r.ArriveAt > r.DepartAt));
    }

    [Fact]
    public void Three_days_have_three_times_the_runs_of_one_when_the_week_is_uniform()
    {
        // Monday to Wednesday: no weekday-restricted service in the seed skips these days.
        var one = Build(new DateOnly(2026, 10, 26), new DateOnly(2026, 10, 26)).Runs.Count;
        var three = Build(new DateOnly(2026, 10, 26), new DateOnly(2026, 10, 28)).Runs.Count;
        Assert.Equal(one * 3, three);
    }

    [Fact]
    public void Weekday_restricted_service_skips_its_off_day()
    {
        var rasdhoo = Data.Services.Single(s => s.Slug == "mtcc-ferry-male-rasdhoo-out");
        Assert.DoesNotContain(5, rasdhoo.DaysOfWeek); // Friday
        var friday = Build(new DateOnly(2026, 10, 30), new DateOnly(2026, 10, 30));
        Assert.DoesNotContain(friday.Runs, r => r.ServiceId == rasdhoo.Id);
    }

    [Fact]
    public void Synthetic_flag_cutoff_and_provider_travel_with_every_run()
    {
        var snap = Build(new DateOnly(2026, 10, 27), new DateOnly(2026, 10, 27));
        Assert.All(snap.Runs, r =>
        {
            Assert.True(r.Synthetic);
            Assert.False(string.IsNullOrEmpty(r.ProviderId));
            Assert.True(r.CheckInCutoffMin >= 0);
        });
        Assert.Contains(snap.Runs, r => r.Mode == TransportMode.DomesticFlight && r.CheckInCutoffMin == 60);
    }

    [Fact]
    public void Transfers_include_taxi_between_all_zone_pairs_in_both_directions()
    {
        var snap = Build(new DateOnly(2026, 10, 27), new DateOnly(2026, 10, 27));
        var zones = Data.Places.Where(p => p.ZoneKey is not null).ToList();
        var taxis = snap.Transfers.Where(t => t.Mode == TransportMode.Taxi).ToList();
        Assert.Equal(zones.Count * (zones.Count - 1), taxis.Count);
        var male = zones.Single(z => z.ZoneKey == "male");
        var hulhumale = zones.Single(z => z.ZoneKey == "hulhumale-1");
        var there = taxis.Single(t => t.From == male.Id && t.To == hulhumale.Id);
        var back = taxis.Single(t => t.From == hulhumale.Id && t.To == male.Id);
        Assert.Equal(there.Fare, back.Fare);
        Assert.Equal(PricingKind.Fixed, there.Pricing);
    }

    [Fact]
    public void Walk_links_exist_between_the_ferry_terminal_and_the_airport()
    {
        var snap = Build(new DateOnly(2026, 10, 27), new DateOnly(2026, 10, 27));
        var jetty = Data.Places.Single(p => p.Slug == "jty-velana").Id;
        var airport = Data.Places.Single(p => p.Code == "MLE").Id;
        Assert.Contains(snap.Transfers, t => t.From == jetty && t.To == airport && t.Mode == TransportMode.Walk);
        Assert.Contains(snap.Transfers, t => t.From == airport && t.To == jetty && t.Mode == TransportMode.Walk);
    }

    [Fact]
    public void Same_catalog_gives_identical_snapshots()
    {
        var a = Build(new DateOnly(2026, 10, 27), new DateOnly(2026, 10, 28));
        var b = Build(new DateOnly(2026, 10, 27), new DateOnly(2026, 10, 28));
        Assert.Equal(a.Version, b.Version);
        Assert.Equal(a.Runs.Select(r => r.ServiceRunId), b.Runs.Select(r => r.ServiceRunId));
    }

    [Fact]
    public void Every_route_can_be_made_end_to_end_island_to_island()
    {
        // Maafushi -> Malé (speedboat) -> Velana airport (ferry) -> Gan (flight) must be connected by runs and transfers.
        var snap = Build(new DateOnly(2026, 10, 27), new DateOnly(2026, 10, 27));
        PlaceRef(snap, "maafushi", out var maafushi);
        var reachable = new HashSet<Domain.Ids.PlaceId> { maafushi };
        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var r in snap.Runs) changed |= reachable.Contains(r.From) && reachable.Add(r.To);
            foreach (var t in snap.Transfers) changed |= reachable.Contains(t.From) && reachable.Add(t.To);
        }
        Assert.Contains(Data.Places.Single(p => p.Slug == "apt-gan").Id, reachable);
        Assert.Contains(Data.Places.Single(p => p.Slug == "isl-gan").Id, reachable);
    }

    private static void PlaceRef(TimetableSnapshot snap, string slug, out Domain.Ids.PlaceId id) =>
        id = Data.Places.Single(p => p.Slug == slug).Id;

    [Fact]
    public async Task Provider_rejects_inverted_ranges_and_uses_the_cache()
    {
        var cache = new CatalogCache(new StaticCatalogDataSource(Data), new TestClock(Now));
        var provider = new CatalogSnapshotProvider(cache, new TestClock(Now));
        await Assert.ThrowsAsync<ArgumentException>(() => provider.GetSnapshotAsync(new DateOnly(2026, 10, 28), new DateOnly(2026, 10, 27), TestContext.Current.CancellationToken));
        var snap = await provider.GetSnapshotAsync(new DateOnly(2026, 10, 27), new DateOnly(2026, 10, 27), TestContext.Current.CancellationToken);
        Assert.Equal(Now, snap.GeneratedAt);
        Assert.Equal(Data.Places.Count, snap.Places.Count);
    }

    [Fact]
    public async Task Reliability_is_a_prior_with_zero_samples_and_unknown_services_have_none()
    {
        var cache = new CatalogCache(new StaticCatalogDataSource(Data), TimeProvider.System);
        var stats = new CatalogReliabilityStats(cache);
        var flight = Data.Services.First(s => s.Mode == TransportMode.DomesticFlight);
        var stat = await stats.GetAsync(flight.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(stat);
        Assert.Equal(0, stat.Samples);
        Assert.InRange(stat.OnTimeRate, 0, 1);
        Assert.Null(await stats.GetAsync(Domain.Ids.ServiceId.New(), TestContext.Current.CancellationToken));
    }
}

public class CatalogCacheTests
{
    private sealed class CountingSource(Func<int, CatalogData> make) : ICatalogDataSource
    {
        public int Loads { get; private set; }

        public bool Fail { get; set; }

        public Task<CatalogData> LoadAsync(CancellationToken ct)
        {
            if (Fail) throw new InvalidOperationException("database down");
            return Task.FromResult(make(++Loads));
        }
    }

    [Fact]
    public async Task Loads_once_within_the_ttl_and_again_after_it()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero));
        var source = new CountingSource(n => CatalogData.Empty with { Version = $"v{n}" });
        var cache = new CatalogCache(source, clock) { Ttl = TimeSpan.FromMinutes(5) };
        Assert.Equal("v1", (await cache.GetAsync(TestContext.Current.CancellationToken)).Version);
        clock.Advance(TimeSpan.FromMinutes(4));
        Assert.Equal("v1", (await cache.GetAsync(TestContext.Current.CancellationToken)).Version);
        clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal("v2", (await cache.GetAsync(TestContext.Current.CancellationToken)).Version);
    }

    [Fact]
    public async Task Invalidate_forces_a_reload()
    {
        var source = new CountingSource(n => CatalogData.Empty with { Version = $"v{n}" });
        var cache = new CatalogCache(source, TimeProvider.System);
        await cache.GetAsync(TestContext.Current.CancellationToken);
        cache.Invalidate();
        Assert.Equal("v2", (await cache.GetAsync(TestContext.Current.CancellationToken)).Version);
    }

    [Fact]
    public async Task A_failed_refresh_keeps_serving_the_previous_catalog()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero));
        var source = new CountingSource(n => CatalogData.Empty with { Version = $"v{n}" });
        var cache = new CatalogCache(source, clock) { Ttl = TimeSpan.FromMinutes(1) };
        await cache.GetAsync(TestContext.Current.CancellationToken);
        source.Fail = true;
        clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal("v1", (await cache.GetAsync(TestContext.Current.CancellationToken)).Version);
    }

    [Fact]
    public async Task First_load_failure_is_not_hidden()
    {
        var source = new CountingSource(_ => CatalogData.Empty) { Fail = true };
        var cache = new CatalogCache(source, TimeProvider.System);
        await Assert.ThrowsAsync<InvalidOperationException>(() => cache.GetAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Concurrent_first_requests_trigger_a_single_load()
    {
        var source = new CountingSource(_ => CatalogData.Empty);
        var cache = new CatalogCache(source, TimeProvider.System);
        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => cache.GetAsync(TestContext.Current.CancellationToken)));
        Assert.Equal(1, source.Loads);
    }
}
