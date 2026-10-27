using Dhathuru.Domain.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Transport;

namespace Dhathuru.Integrations.Status;

public sealed record ServiceStatusDto(string ServiceId, string? ServiceRunId, string State, int DelayMinutes, DateTimeOffset UpdatedAt, string Source);

public static class StatusEndpoints
{
    /// <summary>GET /status/services/{serviceId}?serviceRunId= (live or simulated status, or "unknown" when only the timetable is known).</summary>
    public static RouteGroupBuilder MapStatusEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/status/services/{serviceId}", async (ServiceStatusService status, string serviceId, string? serviceRunId, CancellationToken ct) =>
            {
                if (!ServiceId.TryParse(serviceId, out var id)) throw ProblemException.NotFound("SERVICE_NOT_FOUND");
                var result = await status.GetAsync(id, serviceRunId, ct) ?? throw ProblemException.NotFound("SERVICE_NOT_FOUND");
                return Results.Ok(new ServiceStatusDto(
                    result.ServiceId.Value, result.ServiceRunId.Length == 0 ? null : result.ServiceRunId, ToWire(result.State),
                    result.DelayMinutes, result.UpdatedAt, result.Source));
            })
            .WithName("getServiceStatus")
            .AllowAnonymous();
        return group;
    }

    internal static string ToWire(ServiceRunState state) => state switch
    {
        ServiceRunState.OnTime => "on_time",
        ServiceRunState.Delayed => "delayed",
        ServiceRunState.Cancelled => "cancelled",
        _ => "unknown",
    };
}

public sealed class StatusEndpointModule : IEndpointModule
{
    public void MapEndpoints(RouteGroupBuilder v1) => v1.MapStatusEndpoints();
}
