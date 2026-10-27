using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Pricing;
using Dhathuru.Domain.Providers;
using Dhathuru.Domain.Transport;

namespace Dhathuru.Integrations.Providers;

/// <summary>
/// A pretend operator system on top of the catalog timetable: seat inventory with believable base occupancy, holds that
/// expire, idempotent confirm and cancel, refunds by time to departure and scripted disruptions.
/// All state is in memory and resets on restart. It models the behaviour a real integration must offer, not any real operator.
/// </summary>
public sealed class MockTransportProvider(string providerId, TimetableDirectory directory, DisruptionBoard disruptions, TimeProvider time)
    : ITransportProvider, ISourceAware
{
    public const string RunNotFound = "RUN_NOT_FOUND";
    public const string RunDeparted = "RUN_DEPARTED";
    public const string RunCancelled = "RUN_CANCELLED";
    public const string SoldOut = "SOLD_OUT";
    public const string HoldNotFound = "HOLD_NOT_FOUND";
    public const string HoldExpired = "HOLD_EXPIRED";
    public const string InvalidPassengers = "INVALID_PASSENGERS";
    public const string ProviderRejected = "PROVIDER_REJECTED";
    public const string BookingNotFound = "BOOKING_NOT_FOUND";

    private readonly object gate = new();
    private readonly Dictionary<string, RunInventory> inventory = [];
    private readonly Dictionary<string, Hold> holds = [];
    private readonly Dictionary<string, Booking> bookings = [];
    private readonly Dictionary<string, HoldResult> holdReplays = [];
    private readonly Dictionary<string, ConfirmResult> confirmReplays = [];
    private readonly Dictionary<string, CancelResult> cancelReplays = [];
    private readonly ConcurrentDictionary<string, byte> failConfirm = new();

    public string ProviderId => providerId;

    public ProviderKind Kind => ProviderKind.Mock;

    /// <summary><c>simulated</c> while any disruption is scripted, otherwise <c>mock</c>.</summary>
    public string LastSource => disruptions.HasAny ? "simulated" : "mock";

    /// <summary>Make the next confirm of this run fail once (to exercise compensation in a multi-leg booking).</summary>
    public void FailNextConfirm(string serviceRunId) => failConfirm[serviceRunId] = 0;

    public static int CapacityFor(TransportMode mode) => mode switch
    {
        TransportMode.Ferry => 150,
        TransportMode.Speedboat => 40,
        TransportMode.DomesticFlight => 72,
        TransportMode.Seaplane => 15,
        TransportMode.Bus => 60,
        TransportMode.Taxi => 4,
        _ => 20,
    };

    public async Task<IReadOnlyList<ServiceDeparture>> GetDeparturesAsync(DepartureQuery q, CancellationToken ct)
    {
        var scheduled = await directory.DeparturesAsync(providerId, q, ct);
        var result = new List<ServiceDeparture>(scheduled.Count);
        foreach (var d in scheduled)
        {
            if (disruptions.For(d.ServiceRunId, d.ServiceId) is { State: ServiceRunState.Cancelled }) continue;
            if (d.DepartAt <= time.GetUtcNow()) continue;
            var run = await directory.ResolveAsync(providerId, d.ServiceRunId, ct);
            if (run is null) continue;
            int left;
            lock (gate) left = SeatsLeft(run);
            result.Add(d with { SeatsLeft = left });
        }
        return result;
    }

    public async Task<Availability> GetAvailabilityAsync(AvailabilityQuery q, CancellationToken ct)
    {
        var run = await directory.ResolveAsync(providerId, q.ServiceRunId, ct);
        if (run is null || q.Passengers < 1 || run.DepartAt <= time.GetUtcNow() || IsCancelled(run)) return new Availability(false, 0);
        int left;
        lock (gate) left = SeatsLeft(run);
        return new Availability(left >= q.Passengers, left, run.FarePerPassenger * q.Passengers);
    }

    public async Task<HoldResult> HoldAsync(HoldRequest r, CancellationToken ct)
    {
        var run = await directory.ResolveAsync(providerId, r.ServiceRunId, ct);
        lock (gate)
        {
            if (holdReplays.TryGetValue(r.IdempotencyKey, out var replay)) return replay;
            var result = TryHold(run, r);
            // Failures are not remembered: a retry after the cause is fixed (e.g. seats freed) must be able to succeed.
            if (result.Success) holdReplays[r.IdempotencyKey] = result;
            return result;
        }
    }

    public Task<ConfirmResult> ConfirmAsync(ConfirmRequest r, CancellationToken ct)
    {
        lock (gate)
        {
            if (confirmReplays.TryGetValue(r.IdempotencyKey, out var replay)) return Task.FromResult(replay);
            ConfirmResult result;
            if (!holds.TryGetValue(r.HoldReference, out var hold)) result = new ConfirmResult(false, FailureCode: HoldNotFound);
            else if (hold.ExpiresAt <= time.GetUtcNow() && !hold.Confirmed) result = new ConfirmResult(false, FailureCode: HoldExpired);
            else if (failConfirm.TryRemove(hold.RunId, out _)) result = new ConfirmResult(false, FailureCode: ProviderRejected);
            else
            {
                hold.Confirmed = true;
                var reference = hold.ProviderReference ??= $"pvr_{providerId}_{Ulid.NewString()}";
                bookings[reference] = new Booking(hold.RunId, hold.Passengers, hold.Fare, hold.DepartAt, false);
                result = new ConfirmResult(true, reference);
            }
            if (result.Success) confirmReplays[r.IdempotencyKey] = result;
            return Task.FromResult(result);
        }
    }

    public Task<CancelResult> CancelAsync(CancelRequest r, CancellationToken ct)
    {
        lock (gate)
        {
            if (cancelReplays.TryGetValue(r.IdempotencyKey, out var replay)) return Task.FromResult(replay);
            CancelResult result;
            if (!bookings.TryGetValue(r.ProviderReference, out var booking)) result = new CancelResult(false, FailureCode: BookingNotFound);
            else if (booking.Cancelled) result = new CancelResult(true, new Money(0));
            else
            {
                bookings[r.ProviderReference] = booking with { Cancelled = true };
                result = new CancelResult(true, Refund(booking, time.GetUtcNow()));
            }
            if (result.Success) cancelReplays[r.IdempotencyKey] = result;
            return Task.FromResult(result);
        }
    }

    public async Task<ServiceStatus> GetStatusAsync(string serviceRunId, CancellationToken ct)
    {
        var run = await directory.ResolveAsync(providerId, serviceRunId, ct);
        if (run is null) return new ServiceStatus(serviceRunId, ServiceRunState.Unknown, 0, time.GetUtcNow());
        var d = disruptions.For(serviceRunId, run.Service.Id);
        return d is null
            ? new ServiceStatus(serviceRunId, ServiceRunState.OnTime, 0, time.GetUtcNow())
            : new ServiceStatus(serviceRunId, d.State, d.DelayMinutes, d.SetAt);
    }

    /// <summary>Refund policy (synthetic): 100% until 2 h before departure, 50% until 30 min before, then nothing.</summary>
    public static Money Refund(Money paid, DateTimeOffset departAt, DateTimeOffset now)
    {
        var untilDeparture = departAt - now;
        if (untilDeparture >= TimeSpan.FromHours(2)) return paid;
        if (untilDeparture >= TimeSpan.FromMinutes(30)) return new Money(paid.AmountMinor / 2, paid.Currency);
        return new Money(0, paid.Currency);
    }

    private static Money Refund(Booking b, DateTimeOffset now) => Refund(b.Fare * b.Passengers, b.DepartAt, now);

    private HoldResult TryHold(ResolvedRun? run, HoldRequest r)
    {
        if (r.Passengers < 1) return new HoldResult(false, FailureCode: InvalidPassengers);
        if (run is null) return new HoldResult(false, FailureCode: RunNotFound);
        var now = time.GetUtcNow();
        if (run.DepartAt <= now) return new HoldResult(false, FailureCode: RunDeparted);
        if (IsCancelled(run)) return new HoldResult(false, FailureCode: RunCancelled);
        if (SeatsLeft(run) < r.Passengers) return new HoldResult(false, FailureCode: SoldOut);

        var reference = $"hld_{providerId}_{Ulid.NewString()}";
        var expires = now + r.HoldFor;
        holds[reference] = new Hold(run.ServiceRunId, r.Passengers, expires, run.FarePerPassenger, run.DepartAt);
        return new HoldResult(true, reference, expires);
    }

    private bool IsCancelled(ResolvedRun run) => disruptions.For(run.ServiceRunId, run.Service.Id) is { State: ServiceRunState.Cancelled };

    private int SeatsLeft(ResolvedRun run)
    {
        var now = time.GetUtcNow();
        var baseline = Baseline(run);
        var confirmed = holds.Values.Where(h => h.RunId == run.ServiceRunId && h.Confirmed && !IsCancelledBooking(h)).Sum(h => h.Passengers);
        var held = holds.Values.Where(h => h.RunId == run.ServiceRunId && !h.Confirmed && h.ExpiresAt > now).Sum(h => h.Passengers);
        return Math.Max(0, CapacityFor(run.Service.Mode) - baseline - confirmed - held);
    }

    private bool IsCancelledBooking(Hold h) => h.ProviderReference is { } r && bookings.TryGetValue(r, out var b) && b.Cancelled;

    /// <summary>Deterministic pseudo-occupancy of 15-92% so demos show fuller and emptier runs, stable across restarts.</summary>
    private int Baseline(ResolvedRun run)
    {
        if (run.Service.Mode == TransportMode.Taxi) return 0; // on-demand: a car is always available until booked
        if (inventory.TryGetValue(run.ServiceRunId, out var inv)) return inv.Baseline;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(run.ServiceRunId));
        var fraction = 0.15 + hash[0] / 255.0 * 0.77;
        var baseline = (int)Math.Round(CapacityFor(run.Service.Mode) * fraction);
        inventory[run.ServiceRunId] = new RunInventory(baseline);
        return baseline;
    }

    private sealed record RunInventory(int Baseline);

    private sealed class Hold(string runId, int passengers, DateTimeOffset expiresAt, Money fare, DateTimeOffset departAt)
    {
        public string RunId { get; } = runId;

        public int Passengers { get; } = passengers;

        public DateTimeOffset ExpiresAt { get; } = expiresAt;

        public Money Fare { get; } = fare;

        public DateTimeOffset DepartAt { get; } = departAt;

        public bool Confirmed { get; set; }

        public string? ProviderReference { get; set; }
    }

    private sealed record Booking(string RunId, int Passengers, Money Fare, DateTimeOffset DepartAt, bool Cancelled);
}
