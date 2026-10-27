namespace Dhathuru.Domain.Seams;

/// <summary>
/// Applies one module's EF migrations. Each module registers its own (<c>services.AddSingleton&lt;IModuleMigrator, ...&gt;</c>);
/// the API host runs every registered migrator at startup when <c>Database:MigrateOnStartup</c> is true.
/// Production deployments run the same migrators from a release step instead.
/// </summary>
public interface IModuleMigrator
{
    /// <summary>For logs, for example "identity".</summary>
    string Name { get; }

    Task MigrateAsync(CancellationToken ct);
}
