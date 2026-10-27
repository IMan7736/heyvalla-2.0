using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Places;
using Dhathuru.Domain.Pricing;
using Dhathuru.Domain.Transport;

namespace Dhathuru.Domain.Seams;

/// <summary>
/// KANDU to FARU. The trip engine works only on this in-memory snapshot and never touches the database.
/// Initial shape; owners extend it by agreement through a handoff to ATOLL.
/// </summary>
public interface ITimetableSnapshotProvider
{
    /// <summary>Snapshot covering the local Maldives dates [fromDate, toDate] inclusive.</summary>
    Task<TimetableSnapshot> GetSnapshotAsync(DateOnly fromDate, DateOnly toDate, CancellationToken ct);
}

public sealed record TimetableSnapshot(
    string Version,
    DateTimeOffset GeneratedAt,
    IReadOnlyList<SnapshotPlace> Places,
    IReadOnlyList<SnapshotRun> Runs,
    IReadOnlyList<SnapshotTransfer> Transfers,
    IReadOnlyList<SnapshotMinTransfer>? MinTransfers = null);

public sealed record SnapshotPlace(PlaceId Id, PlaceType Type, string Name, GeoPoint Geo, PlaceId? ParentId, bool IsHub, bool Approximate, string? Code = null);

/// <summary>Minimum minutes needed at <c>PlaceId</c> to change from one mode to another. <c>Verified</c> is false when the figure is an estimate.</summary>
public sealed record SnapshotMinTransfer(PlaceId PlaceId, TransportMode FromMode, TransportMode ToMode, int Minutes, bool Verified);

/// <summary>One concrete departure of a service.</summary>
public sealed record SnapshotRun(
    string ServiceRunId,
    ServiceId ServiceId,
    string ProviderId,
    TransportMode Mode,
    PlaceId From,
    PlaceId To,
    DateTimeOffset DepartAt,
    DateTimeOffset ArriveAt,
    Money Fare,
    PricingKind Pricing,
    int CheckInCutoffMin,
    bool Synthetic);

/// <summary>Walking or road link between two places that can be used between legs (taxi stand, jetty to terminal).</summary>
public sealed record SnapshotTransfer(PlaceId From, PlaceId To, TransportMode Mode, int DurationMin, Money Fare, PricingKind Pricing);

/// <summary>Historical reliability, KANDU to FARU. A null result means "no history", never "perfect".</summary>
public interface IReliabilityStats
{
    Task<ReliabilityStat?> GetAsync(ServiceId serviceId, CancellationToken ct);
}

public sealed record ReliabilityStat(ServiceId ServiceId, double OnTimeRate, double P90DelayMin, int Samples, bool Synthetic = false);
