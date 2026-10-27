using Dhathuru.Domain.Seams;
using Microsoft.EntityFrameworkCore;

namespace Dhathuru.Api.Persistence;

internal sealed class PlatformMigrator(IDbContextFactory<PlatformDbContext> factory) : IModuleMigrator
{
    public string Name => "platform";

    public async Task MigrateAsync(CancellationToken ct)
    {
        await using var db = await factory.CreateDbContextAsync(ct);
        await db.Database.MigrateAsync(ct);
    }
}

/// <summary>Runs every module's migrator before the host starts taking traffic (development and tests; production uses a release step).</summary>
internal sealed class MigrationHostedService(
    IEnumerable<IModuleMigrator> migrators,
    IConfiguration configuration,
    ILogger<MigrationHostedService> logger) : IHostedLifecycleService
{
    public async Task StartingAsync(CancellationToken ct)
    {
        if (!configuration.GetValue<bool>("Database:MigrateOnStartup")) return;
        foreach (var migrator in migrators)
        {
            logger.LogInformation("Applying migrations for {Module}", migrator.Name);
            await migrator.MigrateAsync(ct);
        }
    }

    public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken ct) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken ct) => Task.CompletedTask;

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken ct) => Task.CompletedTask;
}

/// <summary>Lets <c>dotnet ef migrations add --project src/Dhathuru.Api --context PlatformDbContext</c> work without running the host.</summary>
internal sealed class PlatformDesignTimeFactory : Microsoft.EntityFrameworkCore.Design.IDesignTimeDbContextFactory<PlatformDbContext>
{
    public PlatformDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        var builder = new DbContextOptionsBuilder<PlatformDbContext>();
        builder.UseNpgsql(
            Dhathuru.Domain.Configuration.DatabaseOptionsExtensions.GetDatabaseOptions(configuration).ToConnectionString(),
            npgsql => npgsql.MigrationsHistoryTable(PlatformDbContext.MigrationsTable, PlatformDbContext.Schema));
        return new PlatformDbContext(builder.Options);
    }
}
