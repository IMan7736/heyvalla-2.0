using Dhathuru.Catalog.Data;
using Dhathuru.Catalog.Persistence;
using Dhathuru.Catalog.Services;
using Microsoft.EntityFrameworkCore;

namespace Dhathuru.Catalog.Tests;

[Collection(PostgisCollection.Name)]
public class CatalogDatabaseTests(PostgisFixture fixture)
{
    private static readonly CatalogData Seed = TestSupport.LoadSeed();

    private async Task<CatalogDbContext> FreshDatabaseAsync()
    {
        var db = fixture.NewContext();
        await db.Database.MigrateAsync(TestContext.Current.CancellationToken);
        return db;
    }

    [Fact]
    public async Task Migration_creates_the_catalog_schema_with_postgis_geometry()
    {
        await using var db = await FreshDatabaseAsync();
        var type = await db.Database.SqlQueryRaw<string>(
            "select udt_name as \"Value\" from information_schema.columns where table_schema='catalog' and table_name='places' and column_name='Location'").SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("geometry", type);
        Assert.Empty(await db.Database.GetPendingMigrationsAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task First_import_adds_everything_and_the_second_changes_nothing()
    {
        await using var db = await FreshDatabaseAsync();
        var first = await CatalogImporter.ImportAsync(db, Seed, TestContext.Current.CancellationToken);
        Assert.True(first.Added >= Seed.Places.Count + Seed.Services.Count);
        Assert.Equal(0, first.Removed);

        // a fresh context (like a second CLI run) must not see phantom changes
        await using var fresh = fixture.NewContextFor(db);
        var second = await CatalogImporter.ImportAsync(fresh, Seed, TestContext.Current.CancellationToken);
        Assert.Equal(new ImportReport(0, 0, 0), second);
        Assert.False(second.Changed);
    }

    [Fact]
    public async Task Import_updates_changed_rows_and_removes_rows_missing_from_the_seed()
    {
        await using var db = await FreshDatabaseAsync();
        await CatalogImporter.ImportAsync(db, Seed, TestContext.Current.CancellationToken);

        var trimmed = Seed with { Services = [.. Seed.Services.Skip(1)] };
        var changed = trimmed with { Operators = [.. trimmed.Operators.Select((o, i) => i == 0 ? o with { Website = "https://example.invalid" } : o)] };
        var report = await CatalogImporter.ImportAsync(db, changed, TestContext.Current.CancellationToken);
        Assert.Equal(1, report.Removed);
        Assert.Equal(1, report.Updated);
        Assert.Equal(Seed.Services.Count - 1, await db.Services.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Database_round_trip_gives_back_the_same_catalog()
    {
        await using var db = await FreshDatabaseAsync();
        await CatalogImporter.ImportAsync(db, Seed, TestContext.Current.CancellationToken);
        var loaded = await DbCatalogDataSource.LoadAsync(db, TestContext.Current.CancellationToken);

        Assert.Equal(Seed.Version, loaded.Version);
        Assert.Equal(Seed.Places.Count, loaded.Places.Count);
        var male = loaded.Places.Single(p => p.Slug == "male");
        var seedMale = Seed.Places.Single(p => p.Slug == "male");
        Assert.Equal(seedMale.Geo.Lat, male.Geo.Lat, 9);
        Assert.Equal(seedMale.Geo.Lng, male.Geo.Lng, 9);
        Assert.Equal(seedMale.Name["dv"], male.Name["dv"]);

        var flight = Seed.Services.First(s => s.Mode == Domain.Transport.TransportMode.DomesticFlight);
        var back = loaded.Services.Single(s => s.Id == flight.Id);
        Assert.Equal(flight.Times, back.Times);
        Assert.Equal(flight.Fare, back.Fare);
        Assert.Equal(flight.DaysOfWeek, back.DaysOfWeek);
        Assert.Equal(flight.CheckInCutoffMin, back.CheckInCutoffMin);

        Assert.Equal(Seed.Taxi!.Fares.Count, loaded.Taxi!.Fares.Count);
        Assert.Equal(Seed.Taxi.Durations.Count, loaded.Taxi.Durations.Count);
        Assert.Equal(Seed.Taxi.DurationFallbackMin.PerKm, loaded.Taxi.DurationFallbackMin.PerKm, 9);
    }

    [Fact]
    public async Task Snapshot_from_the_database_equals_snapshot_from_the_seed()
    {
        await using var db = await FreshDatabaseAsync();
        await CatalogImporter.ImportAsync(db, Seed, TestContext.Current.CancellationToken);
        var loaded = await DbCatalogDataSource.LoadAsync(db, TestContext.Current.CancellationToken);

        var day = new DateOnly(2026, 10, 27);
        var now = new DateTimeOffset(2026, 10, 26, 0, 0, 0, TimeSpan.Zero);
        var fromSeed = CatalogSnapshotProvider.Build(new CatalogIndex(Seed), day, day, now);
        var fromDb = CatalogSnapshotProvider.Build(new CatalogIndex(loaded), day, day, now);

        Assert.Equal(fromSeed.Version, fromDb.Version);
        Assert.Equal(fromSeed.Runs.Select(r => r.ServiceRunId), fromDb.Runs.Select(r => r.ServiceRunId));
        Assert.Equal(fromSeed.Transfers.Count, fromDb.Transfers.Count);
    }
}
