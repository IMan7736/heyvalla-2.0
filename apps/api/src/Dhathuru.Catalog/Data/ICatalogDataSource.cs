namespace Dhathuru.Catalog.Data;

/// <summary>Where the in-memory catalog comes from: the database in production, seed files in tests and offline demos.</summary>
public interface ICatalogDataSource
{
    Task<CatalogData> LoadAsync(CancellationToken ct);
}

public sealed class SeedFileCatalogDataSource(string seedDirectory) : ICatalogDataSource
{
    public Task<CatalogData> LoadAsync(CancellationToken ct) => Task.FromResult(SeedFiles.Load(seedDirectory));
}

public sealed class StaticCatalogDataSource(CatalogData data) : ICatalogDataSource
{
    public Task<CatalogData> LoadAsync(CancellationToken ct) => Task.FromResult(data);
}
