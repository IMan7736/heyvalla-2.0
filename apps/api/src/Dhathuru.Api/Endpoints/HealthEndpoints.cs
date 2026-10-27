using System.Reflection;
using Dhathuru.Api.Infrastructure;

namespace Dhathuru.Api.Endpoints;

public static class HealthEndpoints
{
    public static RouteGroupBuilder MapHealthEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/health", async (IDatabaseProbe database, TimeProvider time, CancellationToken ct) =>
        {
            var databaseUp = await database.IsAvailableAsync(ct);
            var body = new HealthResponse(
                databaseUp ? "ok" : "degraded",
                Version,
                time.GetUtcNow(),
                new Dictionary<string, string> { ["database"] = databaseUp ? "up" : "down" });
            return databaseUp ? Results.Ok(body) : Results.Json(body, statusCode: StatusCodes.Status503ServiceUnavailable);
        })
        .WithName("getHealth")
        .AllowAnonymous();

        // Liveness: the process is up. Readiness: the database answers. Orchestrators use these two.
        group.MapGet("/health/live", () => Results.Ok(new { status = "ok" })).AllowAnonymous();
        group.MapGet("/health/ready", async (IDatabaseProbe database, CancellationToken ct) =>
                await database.IsAvailableAsync(ct)
                    ? Results.Ok(new { status = "ok" })
                    : Results.Json(new { status = "degraded" }, statusCode: StatusCodes.Status503ServiceUnavailable))
            .AllowAnonymous();
        return group;
    }

    private static string Version { get; } =
        (Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0")
        .Split('+')[0];

    public sealed record HealthResponse(string Status, string Version, DateTimeOffset Time, IReadOnlyDictionary<string, string> Checks);
}
