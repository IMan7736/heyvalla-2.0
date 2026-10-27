using Dhathuru.Catalog.Data;

namespace Dhathuru.Catalog.Services;

/// <summary>
/// Holds the loaded catalog in memory and refreshes it after <see cref="Ttl"/> or on <see cref="Invalidate"/>.
/// One load at a time; readers never see a half built catalog. A failed refresh keeps serving the previous one.
/// </summary>
public sealed class CatalogCache(ICatalogDataSource source, TimeProvider time)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private CatalogIndex? current;
    private DateTimeOffset loadedAt;

    public TimeSpan Ttl { get; init; } = TimeSpan.FromMinutes(5);

    public void Invalidate() => loadedAt = DateTimeOffset.MinValue;

    public async Task<CatalogIndex> GetAsync(CancellationToken ct)
    {
        var existing = current;
        if (existing is not null && time.GetUtcNow() - loadedAt < Ttl) return existing;

        await gate.WaitAsync(ct);
        try
        {
            if (current is not null && time.GetUtcNow() - loadedAt < Ttl) return current;
            try
            {
                var data = await source.LoadAsync(ct);
                current = new CatalogIndex(data);
                loadedAt = time.GetUtcNow();
            }
            catch (Exception) when (current is not null)
            {
                // Keep serving the previous catalog and retry on the next request after a short pause.
                loadedAt = time.GetUtcNow() - Ttl + TimeSpan.FromSeconds(15);
            }
            return current!;
        }
        finally
        {
            gate.Release();
        }
    }
}

/// <summary>Lookups over an immutable <see cref="CatalogData"/>.</summary>
public sealed class CatalogIndex
{
    public CatalogIndex(CatalogData data)
    {
        Data = data;
        PlacesById = data.Places.ToDictionary(p => p.Id);
        ServicesById = data.Services.ToDictionary(s => s.Id);
        OperatorsById = data.Operators.ToDictionary(o => o.Id);
    }

    public CatalogData Data { get; }

    public IReadOnlyDictionary<Dhathuru.Domain.Ids.PlaceId, CatalogPlace> PlacesById { get; }

    public IReadOnlyDictionary<Dhathuru.Domain.Ids.ServiceId, CatalogService> ServicesById { get; }

    public IReadOnlyDictionary<Dhathuru.Domain.Ids.OperatorId, CatalogOperator> OperatorsById { get; }

    public string Version => Data.Version;
}
