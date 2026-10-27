using System.Collections.Concurrent;
using Dhathuru.Domain.Providers;
using Dhathuru.Domain.Transport;
using Microsoft.Extensions.Logging;

namespace Dhathuru.Integrations.Providers;

public sealed class ResilientOptions
{
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(4);

    public int FailureThreshold { get; set; } = 3;

    public TimeSpan OpenFor { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan DepartureCacheTtl { get; set; } = TimeSpan.FromSeconds(30);
}

/// <summary>
/// Wraps a live adapter. Every call has a timeout and goes through a circuit breaker. Reads (departures, availability, status)
/// are cached briefly and fall back to the static timetable when the live system fails, so planning keeps working.
/// Writes (hold, confirm, cancel) never fall back: a static timetable cannot sell a seat, so they report <see cref="Unavailable"/>.
/// <see cref="LastSource"/> says whether the most recent read was <c>live</c> or fell back to <c>static</c>.
/// </summary>
public sealed class ResilientProvider(
    ITransportProvider live, ITransportProvider fallback, ResilientOptions options, TimeProvider time, ILogger<ResilientProvider>? logger = null)
    : ITransportProvider, ISourceAware
{
    public const string Unavailable = "PROVIDER_UNAVAILABLE";

    private readonly CircuitBreaker breaker = new(options.FailureThreshold, options.OpenFor, time);
    private readonly ConcurrentDictionary<string, (DateTimeOffset At, IReadOnlyList<ServiceDeparture> Value)> departureCache = new();
    private string lastSource = "live";

    public string ProviderId => live.ProviderId;

    public ProviderKind Kind => ProviderKind.Live;

    public string LastSource => lastSource;

    public bool CircuitOpen => breaker.IsOpen;

    public async Task<IReadOnlyList<ServiceDeparture>> GetDeparturesAsync(DepartureQuery q, CancellationToken ct)
    {
        var key = $"{q.From}|{q.To}|{q.WindowStart:O}|{q.WindowEnd:O}";
        if (departureCache.TryGetValue(key, out var cached) && time.GetUtcNow() - cached.At < options.DepartureCacheTtl)
        {
            return cached.Value;
        }
        var result = await ReadAsync(token => live.GetDeparturesAsync(q, token), token => fallback.GetDeparturesAsync(q, token), ct);
        if (lastSource == "live") departureCache[key] = (time.GetUtcNow(), result);
        return result;
    }

    public Task<Availability> GetAvailabilityAsync(AvailabilityQuery q, CancellationToken ct) =>
        ReadAsync(token => live.GetAvailabilityAsync(q, token), token => fallback.GetAvailabilityAsync(q, token), ct);

    public Task<ServiceStatus> GetStatusAsync(string serviceRunId, CancellationToken ct) =>
        ReadAsync(token => live.GetStatusAsync(serviceRunId, token), token => fallback.GetStatusAsync(serviceRunId, token), ct);

    public async Task<HoldResult> HoldAsync(HoldRequest r, CancellationToken ct) =>
        await WriteAsync(token => live.HoldAsync(r, token), new HoldResult(false, FailureCode: Unavailable), ct);

    public async Task<ConfirmResult> ConfirmAsync(ConfirmRequest r, CancellationToken ct) =>
        await WriteAsync(token => live.ConfirmAsync(r, token), new ConfirmResult(false, FailureCode: Unavailable), ct);

    public async Task<CancelResult> CancelAsync(CancelRequest r, CancellationToken ct) =>
        await WriteAsync(token => live.CancelAsync(r, token), new CancelResult(false, FailureCode: Unavailable), ct);

    private async Task<T> ReadAsync<T>(Func<CancellationToken, Task<T>> call, Func<CancellationToken, Task<T>> fallbackCall, CancellationToken ct)
    {
        if (breaker.TryAcquire())
        {
            try
            {
                var value = await WithTimeoutAsync(call, ct);
                breaker.RecordSuccess();
                lastSource = "live";
                return value;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                breaker.RecordFailure();
                logger?.LogWarning(ex, "Live provider {ProviderId} failed; serving the static timetable", ProviderId);
            }
        }
        lastSource = "static";
        return await fallbackCall(ct);
    }

    private async Task<T> WriteAsync<T>(Func<CancellationToken, Task<T>> call, T unavailable, CancellationToken ct)
    {
        if (!breaker.TryAcquire()) return unavailable;
        try
        {
            var value = await WithTimeoutAsync(call, ct);
            breaker.RecordSuccess();
            return value;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            breaker.RecordFailure();
            logger?.LogWarning(ex, "Live provider {ProviderId} failed on a write", ProviderId);
            return unavailable;
        }
    }

    private async Task<T> WithTimeoutAsync<T>(Func<CancellationToken, Task<T>> call, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(options.Timeout);
        try
        {
            return await call(cts.Token);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new TimeoutException($"Provider {ProviderId} did not answer within {options.Timeout.TotalSeconds:0.#} s.");
        }
    }
}
