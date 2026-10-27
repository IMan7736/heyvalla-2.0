using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Pricing;
using Dhathuru.Domain.Transport;

namespace Dhathuru.Domain.Providers;

/// <summary>
/// Adapter seam for every external transport system. Static, mock and live implementations are swapped
/// by configuration. KANDU owns implementations; the shape follows docs/CONTRACT.md.
/// </summary>
public interface ITransportProvider
{
    string ProviderId { get; }

    ProviderKind Kind { get; }

    Task<IReadOnlyList<ServiceDeparture>> GetDeparturesAsync(DepartureQuery q, CancellationToken ct);

    Task<Availability> GetAvailabilityAsync(AvailabilityQuery q, CancellationToken ct);

    Task<HoldResult> HoldAsync(HoldRequest r, CancellationToken ct);

    Task<ConfirmResult> ConfirmAsync(ConfirmRequest r, CancellationToken ct);

    Task<CancelResult> CancelAsync(CancelRequest r, CancellationToken ct);

    Task<ServiceStatus> GetStatusAsync(string serviceRunId, CancellationToken ct);
}

/// <summary>Departures leaving <c>From</c> for <c>To</c> inside the half open UTC window [WindowStart, WindowEnd).</summary>
public sealed record DepartureQuery(PlaceId From, PlaceId To, DateTimeOffset WindowStart, DateTimeOffset WindowEnd);

public sealed record ServiceDeparture(
    string ServiceRunId,
    ServiceId ServiceId,
    PlaceId From,
    PlaceId To,
    DateTimeOffset DepartAt,
    DateTimeOffset ArriveAt,
    Money Fare,
    PricingKind Pricing,
    int? SeatsLeft = null);

public sealed record AvailabilityQuery(string ServiceRunId, int Passengers);

public sealed record Availability(bool Available, int? SeatsLeft = null, Money? Price = null);

/// <summary>Idempotent: the same key must return the same hold.</summary>
public sealed record HoldRequest(string ServiceRunId, int Passengers, string IdempotencyKey, TimeSpan HoldFor);

public sealed record HoldResult(bool Success, string? HoldReference = null, DateTimeOffset? ExpiresAt = null, string? FailureCode = null);

public sealed record ConfirmRequest(string HoldReference, string IdempotencyKey);

public sealed record ConfirmResult(bool Success, string? ProviderReference = null, string? FailureCode = null);

public sealed record CancelRequest(string ProviderReference, string IdempotencyKey);

public sealed record CancelResult(bool Success, Money? Refund = null, string? FailureCode = null);

public sealed record ServiceStatus(string ServiceRunId, ServiceRunState State, int DelayMinutes, DateTimeOffset UpdatedAt);
