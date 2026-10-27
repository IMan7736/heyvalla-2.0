using Dhathuru.Domain.Configuration;
using Npgsql;

namespace Dhathuru.Api.Infrastructure;

/// <summary>Readiness check for the database. Replaceable in tests.</summary>
public interface IDatabaseProbe
{
    Task<bool> IsAvailableAsync(CancellationToken ct);
}

public sealed class NpgsqlDatabaseProbe(IConfiguration configuration) : IDatabaseProbe
{
    public async Task<bool> IsAvailableAsync(CancellationToken ct)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(2));
            await using var connection = new NpgsqlConnection(configuration.GetDatabaseOptions().ToConnectionString());
            await connection.OpenAsync(timeout.Token);
            await using var command = new NpgsqlCommand("SELECT 1", connection);
            await command.ExecuteScalarAsync(timeout.Token);
            return true;
        }
        catch (Exception ex) when (ex is NpgsqlException or InvalidOperationException or OperationCanceledException)
        {
            return false;
        }
    }
}
