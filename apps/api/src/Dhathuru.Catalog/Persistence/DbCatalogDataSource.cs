using System.Text.Json;
using Dhathuru.Catalog.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dhathuru.Catalog.Persistence;

/// <summary>Loads the catalog from Postgres. A short-lived scope per load keeps it usable from singletons.</summary>
public sealed class DbCatalogDataSource(IServiceScopeFactory scopes) : ICatalogDataSource
{
    public async Task<CatalogData> LoadAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        return await LoadAsync(db, ct);
    }

    public static async Task<CatalogData> LoadAsync(CatalogDbContext db, CancellationToken ct)
    {
        var modelParams = await db.ModelParams.AsNoTracking().ToDictionaryAsync(p => p.Key, p => p.JsonValue, ct);
        var fares = await db.TaxiFares.AsNoTracking().ToListAsync(ct);
        var durations = await db.TaxiDurations.AsNoTracking().ToListAsync(ct);
        var version = modelParams.TryGetValue(CatalogRowMapper.VersionKey, out var v) ? JsonSerializer.Deserialize<string>(v) ?? "db" : "db";
        return new CatalogData(
            [.. (await db.Places.AsNoTracking().OrderBy(p => p.Slug).ToListAsync(ct)).Select(CatalogRowMapper.FromRow)],
            [.. (await db.Operators.AsNoTracking().OrderBy(o => o.Slug).ToListAsync(ct)).Select(CatalogRowMapper.FromRow)],
            [.. (await db.Services.AsNoTracking().OrderBy(s => s.Slug).ToListAsync(ct)).Select(CatalogRowMapper.FromRow)],
            [.. (await db.MinTransfers.AsNoTracking().ToListAsync(ct)).Select(CatalogRowMapper.FromRow)],
            [.. (await db.Links.AsNoTracking().ToListAsync(ct)).Select(CatalogRowMapper.FromRow)],
            [.. (await db.ReliabilityPriors.AsNoTracking().ToListAsync(ct)).Select(CatalogRowMapper.FromRow)],
            CatalogRowMapper.TaxiFromRows(fares, durations, modelParams),
            version);
    }
}
