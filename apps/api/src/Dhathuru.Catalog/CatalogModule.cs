using Dhathuru.Catalog.Data;
using Dhathuru.Catalog.Persistence;
using Dhathuru.Catalog.Services;
using Dhathuru.Catalog.Endpoints;
using Dhathuru.Domain.Configuration;
using Dhathuru.Domain.Http;
using Dhathuru.Domain.Seams;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dhathuru.Catalog;

public static class CatalogModule
{
    /// <summary>
    /// Registers the catalog. <c>Catalog:Source</c> is <c>database</c> (default) or <c>seed</c>; with <c>seed</c>,
    /// <c>Catalog:SeedDirectory</c> points at <c>apps/api/seed</c> and no database is needed.
    /// </summary>
    public static IServiceCollection AddCatalogModule(this IServiceCollection services, IConfiguration configuration)
    {
        // Resolved lazily so the host can start (and answer /health) before a password is configured.
        services.AddDbContext<CatalogDbContext>(o => Configure(o, configuration.GetDatabaseOptions().ToConnectionString()));
        services.TryAddSingleton(TimeProvider.System);

        var source = configuration["Catalog:Source"] ?? "database";
        if (string.Equals(source, "seed", StringComparison.OrdinalIgnoreCase))
        {
            var directory = configuration["Catalog:SeedDirectory"] ?? throw new InvalidOperationException("Catalog:SeedDirectory is required when Catalog:Source=seed.");
            services.AddSingleton<ICatalogDataSource>(new SeedFileCatalogDataSource(directory));
        }
        else
        {
            services.AddSingleton<ICatalogDataSource, DbCatalogDataSource>();
        }

        services.AddSingleton<CatalogCache>();
        services.AddSingleton<IEndpointModule, CatalogEndpointModule>();
        services.AddSingleton<ITimetableSnapshotProvider, CatalogSnapshotProvider>();
        services.AddSingleton<IReliabilityStats, CatalogReliabilityStats>();
        return services;
    }

    public static void Configure(DbContextOptionsBuilder options, string connectionString) =>
        options.UseNpgsql(connectionString, npgsql =>
        {
            npgsql.UseNetTopologySuite();
            npgsql.MigrationsHistoryTable(CatalogDbContext.MigrationsTable, CatalogDbContext.Schema);
        });
}

/// <summary>Lets <c>dotnet ef migrations add --project src/Dhathuru.Catalog</c> work without the host.</summary>
public sealed class CatalogDesignTimeFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        var builder = new DbContextOptionsBuilder<CatalogDbContext>();
        CatalogModule.Configure(builder, configuration.GetDatabaseOptions().ToConnectionString());
        return new CatalogDbContext(builder.Options);
    }
}
