using System.Collections.Concurrent;
using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Transport;

namespace Dhathuru.Integrations.Providers;

public sealed record Disruption(ServiceRunState State, int DelayMinutes, DateTimeOffset SetAt);

/// <summary>
/// Scriptable disruptions for demos and tests, shared by every mock provider. A disruption targets one run
/// (<c>svc_...:2026-10-27T02:30Z</c>) or every run of a service. Run level wins over service level.
/// </summary>
public sealed class DisruptionBoard(TimeProvider time)
{
    private readonly ConcurrentDictionary<string, Disruption> byRun = new();
    private readonly ConcurrentDictionary<ServiceId, Disruption> byService = new();

    public void DelayRun(string serviceRunId, int minutes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(minutes, 1);
        byRun[serviceRunId] = new Disruption(ServiceRunState.Delayed, minutes, time.GetUtcNow());
    }

    public void CancelRun(string serviceRunId) =>
        byRun[serviceRunId] = new Disruption(ServiceRunState.Cancelled, 0, time.GetUtcNow());

    public void DelayService(ServiceId id, int minutes)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(minutes, 1);
        byService[id] = new Disruption(ServiceRunState.Delayed, minutes, time.GetUtcNow());
    }

    public void CancelService(ServiceId id) =>
        byService[id] = new Disruption(ServiceRunState.Cancelled, 0, time.GetUtcNow());

    public void Clear()
    {
        byRun.Clear();
        byService.Clear();
    }

    public bool ClearRun(string serviceRunId) => byRun.TryRemove(serviceRunId, out _);

    public bool ClearService(ServiceId id) => byService.TryRemove(id, out _);

    public bool HasAny => !byRun.IsEmpty || !byService.IsEmpty;

    /// <summary>The disruption affecting a run, or null when it runs as scheduled.</summary>
    public Disruption? For(string serviceRunId, ServiceId serviceId) =>
        byRun.TryGetValue(serviceRunId, out var run) ? run : byService.TryGetValue(serviceId, out var svc) ? svc : null;
}
