using Dhathuru.Catalog.Services;
using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Time;
using Dhathuru.Domain.Transport;
using Dhathuru.Integrations.Providers;
using Dhathuru.Domain.Providers;

namespace Dhathuru.Integrations.Status;

/// <summary>
/// Status of a service run for the UI. <c>Source</c> tells the traveller whether this is live data, a mock, a scripted
/// simulation or just the timetable ("scheduled"): <c>static</c> means nothing is known about the day.
/// </summary>
public sealed record ServiceStatusResult(ServiceId ServiceId, string ServiceRunId, ServiceRunState State, int DelayMinutes, DateTimeOffset UpdatedAt, string Source);

public sealed class ServiceStatusService(CatalogCache catalog, IProviderRegistry registry, TimeProvider time)
{
    /// <summary>Null if the service is unknown. Without a run id, reports the next scheduled run (today, then the next 7 days).</summary>
    public async Task<ServiceStatusResult?> GetAsync(ServiceId serviceId, string? serviceRunId, CancellationToken ct)
    {
        var index = await catalog.GetAsync(ct);
        if (!index.ServicesById.TryGetValue(serviceId, out var service)) return null;
        var providerId = index.OperatorsById[service.OperatorId].ProviderId;

        var runId = serviceRunId;
        if (runId is null)
        {
            runId = NextRun(service)?.ServiceRunId;
            if (runId is null) return new ServiceStatusResult(serviceId, "", ServiceRunState.Unknown, 0, time.GetUtcNow(), "static");
        }
        else if (!ServiceCalendar.TryParseRunId(runId.Split('@')[0], out var parsed, out _) || parsed != serviceId)
        {
            return null;
        }

        var provider = registry.Get(providerId);
        var status = await provider.GetStatusAsync(runId, ct);
        var source = provider is ISourceAware aware ? aware.LastSource : provider.Kind.ToString().ToLowerInvariant();
        return new ServiceStatusResult(serviceId, runId, status.State, status.DelayMinutes, status.UpdatedAt, source);
    }

    private ScheduledRun? NextRun(Catalog.Data.CatalogService service)
    {
        var now = time.GetUtcNow();
        var today = MaldivesTime.LocalDate(now);
        for (var i = 0; i < 8; i++)
        {
            var run = ServiceCalendar.RunsFor(service, today.AddDays(i)).FirstOrDefault(r => r.DepartAt > now);
            if (run is not null) return run;
        }
        return null;
    }
}
