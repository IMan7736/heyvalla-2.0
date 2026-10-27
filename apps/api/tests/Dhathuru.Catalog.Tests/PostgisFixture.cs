using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Dhathuru.Catalog.Tests;

/// <summary>One throwaway PostGIS container per test run. If Docker is not available the database tests skip instead of failing.</summary>
public sealed class PostgisFixture : IAsyncLifetime
{
    private PostgreSqlContainer? container;

    public string? ConnectionString { get; private set; }

    private readonly System.Collections.Concurrent.ConcurrentDictionary<CatalogDbContext, string> lastConnection = new();

    public string? UnavailableReason { get; private set; }

    public async ValueTask InitializeAsync()
    {
        try
        {
            container = new PostgreSqlBuilder("postgis/postgis:17-3.5").Build();
            await container.StartAsync();
            ConnectionString = container.GetConnectionString();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            UnavailableReason = $"Docker/PostGIS not available: {ex.GetType().Name}";
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (container is not null) await container.DisposeAsync();
    }

    public CatalogDbContext NewContextFor(CatalogDbContext existing)
    {
        var builder = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<CatalogDbContext>();
        CatalogModule.Configure(builder, lastConnection[existing]);
        return new CatalogDbContext(builder.Options);
    }

    public CatalogDbContext NewContext()
    {
        if (ConnectionString is null) Assert.Skip(UnavailableReason ?? "database unavailable");
        // A fresh database per context keeps tests independent (the default database cannot be dropped while connected).
        var connection = new Npgsql.NpgsqlConnectionStringBuilder(ConnectionString) { Database = $"kandu_{Guid.NewGuid():N}" };
        var builder = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<CatalogDbContext>();
        CatalogModule.Configure(builder, connection.ConnectionString);
        var created = new CatalogDbContext(builder.Options);
        lastConnection[created] = connection.ConnectionString;
        return created;
    }
}

[CollectionDefinition(Name)]
public sealed class PostgisCollection : ICollectionFixture<PostgisFixture>
{
    public const string Name = "postgis";
}
