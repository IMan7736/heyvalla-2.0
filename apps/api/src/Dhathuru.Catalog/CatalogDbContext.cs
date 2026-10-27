using Dhathuru.Catalog.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dhathuru.Catalog;

public sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options) : DbContext(options)
{
    public const string Schema = "catalog";
    public const string MigrationsTable = "__ef_migrations";

    public DbSet<PlaceRow> Places => Set<PlaceRow>();

    public DbSet<OperatorRow> Operators => Set<OperatorRow>();

    public DbSet<ServiceRow> Services => Set<ServiceRow>();

    public DbSet<MinTransferRow> MinTransfers => Set<MinTransferRow>();

    public DbSet<LinkRow> Links => Set<LinkRow>();

    public DbSet<ReliabilityPriorRow> ReliabilityPriors => Set<ReliabilityPriorRow>();

    public DbSet<TaxiFareRow> TaxiFares => Set<TaxiFareRow>();

    public DbSet<TaxiDurationRow> TaxiDurations => Set<TaxiDurationRow>();

    public DbSet<ModelParamRow> ModelParams => Set<ModelParamRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.HasPostgresExtension("postgis");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CatalogDbContext).Assembly);
    }
}
