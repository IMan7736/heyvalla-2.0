using Dhathuru.Catalog.Data;
using Microsoft.EntityFrameworkCore;

namespace Dhathuru.Catalog.Persistence;

public sealed record ImportReport(int Added, int Updated, int Removed)
{
    public bool Changed => Added + Updated + Removed > 0;

    public static ImportReport operator +(ImportReport a, ImportReport b) => new(a.Added + b.Added, a.Updated + b.Updated, a.Removed + b.Removed);
}

/// <summary>
/// Idempotent catalog import: makes the database equal to the given <see cref="CatalogData"/> in one transaction.
/// Running it twice reports no changes the second time. The catalog is fully seed-owned today, so rows missing
/// from the data are removed.
/// </summary>
public static class CatalogImporter
{
    public static async Task<ImportReport> ImportAsync(CatalogDbContext db, CatalogData data, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var report = new ImportReport(0, 0, 0);

        // Places first: other rows refer to them by id (no database foreign keys, so order is for readability).
        report += await SyncAsync(db, db.Places, data.Places.Select(CatalogRowMapper.ToRow), r => r.Id, ct);
        report += await SyncAsync(db, db.Operators, data.Operators.Select(CatalogRowMapper.ToRow), r => r.Id, ct);
        report += await SyncAsync(db, db.Services, data.Services.Select(CatalogRowMapper.ToRow), r => r.Id, ct);
        report += await SyncAsync(db, db.MinTransfers, data.MinTransfers.Select(CatalogRowMapper.ToRow), r => $"{r.PlaceId}|{r.FromMode}|{r.ToMode}", ct);
        report += await SyncAsync(db, db.Links, data.Links.Select(CatalogRowMapper.ToRow), r => $"{r.FromId}|{r.ToId}|{r.Mode}", ct);
        report += await SyncAsync(db, db.ReliabilityPriors, data.ReliabilityPriors.Select(CatalogRowMapper.ToRow), r => r.Mode, ct);
        report += await SyncAsync(db, db.TaxiFares, (data.Taxi?.Fares ?? []).Select(f => new TaxiFareRow { FromZone = f.FromZone, ToZone = f.ToZone, FareMinor = f.FareMinor }),
            r => $"{r.FromZone}|{r.ToZone}", ct);
        report += await SyncAsync(db, db.TaxiDurations,
            (data.Taxi?.Durations ?? []).Select(d => new TaxiDurationRow
            {
                FromZone = d.FromZone, ToZone = d.ToZone, Band = d.Band, Samples = d.Samples, MedianMin = d.MedianMin, P90Min = d.P90Min, MedianKm = d.MedianKm,
            }),
            r => $"{r.FromZone}|{r.ToZone}|{r.Band}", ct);
        report += await SyncAsync(db, db.ModelParams, CatalogRowMapper.ModelParamsOf(data), r => r.Key, ct);

        await tx.CommitAsync(ct);
        return report;
    }

    private static async Task<ImportReport> SyncAsync<TRow>(
        CatalogDbContext db, DbSet<TRow> set, IEnumerable<TRow> desired, Func<TRow, string> key, CancellationToken ct)
        where TRow : class
    {
        var wanted = desired.ToDictionary(key);
        var existing = (await set.ToListAsync(ct)).ToDictionary(key);
        int added = 0, updated = 0, removed = 0;

        foreach (var (k, row) in wanted)
        {
            if (!existing.TryGetValue(k, out var current))
            {
                set.Add(row);
                added++;
                continue;
            }
            var entry = db.Entry(current);
            entry.CurrentValues.SetValues(row);
            if (entry.State == EntityState.Modified) updated++;
        }
        foreach (var (k, row) in existing)
        {
            if (wanted.ContainsKey(k)) continue;
            set.Remove(row);
            removed++;
        }
        await db.SaveChangesAsync(ct);
        return new ImportReport(added, updated, removed);
    }
}
