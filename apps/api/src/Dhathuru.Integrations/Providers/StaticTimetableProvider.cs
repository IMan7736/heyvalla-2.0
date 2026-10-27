using Dhathuru.Domain.Providers;
using Dhathuru.Domain.Transport;

namespace Dhathuru.Integrations.Providers;

/// <summary>
/// Read-only provider backed by the catalog timetable. It knows what is scheduled and nothing about the day:
/// no seats, no live status, no booking. This is also the fallback for live adapters.
/// </summary>
public sealed class StaticTimetableProvider(string providerId, TimetableDirectory directory, TimeProvider time) : ITransportProvider, ISourceAware
{
    public const string ReadOnlyFailure = "PROVIDER_READ_ONLY";

    public string ProviderId => providerId;

    public ProviderKind Kind => ProviderKind.Static;

    public string LastSource => "static";

    public Task<IReadOnlyList<ServiceDeparture>> GetDeparturesAsync(DepartureQuery q, CancellationToken ct) =>
        directory.DeparturesAsync(providerId, q, ct);

    public async Task<Availability> GetAvailabilityAsync(AvailabilityQuery q, CancellationToken ct)
    {
        var run = await directory.ResolveAsync(providerId, q.ServiceRunId, ct);
        // The timetable says the run exists; it cannot say whether a seat is free, so SeatsLeft stays null ("unknown").
        return run is null || run.DepartAt <= time.GetUtcNow()
            ? new Availability(false)
            : new Availability(true, null, run.FarePerPassenger * q.Passengers);
    }

    public Task<HoldResult> HoldAsync(HoldRequest r, CancellationToken ct) => Task.FromResult(new HoldResult(false, FailureCode: ReadOnlyFailure));

    public Task<ConfirmResult> ConfirmAsync(ConfirmRequest r, CancellationToken ct) => Task.FromResult(new ConfirmResult(false, FailureCode: ReadOnlyFailure));

    public Task<CancelResult> CancelAsync(CancelRequest r, CancellationToken ct) => Task.FromResult(new CancelResult(false, FailureCode: ReadOnlyFailure));

    // Honest: a timetable cannot know whether the run is on time. Unknown, not "on time".
    public Task<ServiceStatus> GetStatusAsync(string serviceRunId, CancellationToken ct) =>
        Task.FromResult(new ServiceStatus(serviceRunId, ServiceRunState.Unknown, 0, time.GetUtcNow()));
}

/// <summary>Where the last answer came from: <c>static</c>, <c>mock</c>, <c>simulated</c> or <c>live</c>. Shown in the UI as scheduled vs live.</summary>
public interface ISourceAware
{
    string LastSource { get; }
}
