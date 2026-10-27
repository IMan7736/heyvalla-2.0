using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Pricing;
using Dhathuru.Domain.Seams;
using Dhathuru.Domain.Transport;

namespace Dhathuru.TripEngine.Network;

internal enum HopKind { Scheduled, Flexible }

/// <summary>
/// One movement in real (forward) time. Times are whole minutes since the Unix epoch (UTC); runs also keep the exact
/// instants so output legs carry the provider's own timestamps.
/// </summary>
internal sealed class Hop
{
    public required HopKind Kind { get; init; }

    public required TransportMode Mode { get; init; }

    public required PlaceId From { get; init; }

    public required PlaceId To { get; init; }

    public required long Dep { get; init; }

    public required long Arr { get; init; }

    /// <summary>Per-adult fare for a run; per-vehicle fare for a taxi; zero for walking.</summary>
    public required Money Fare { get; init; }

    public required PricingKind Pricing { get; init; }

    public SnapshotRun? Run { get; init; }

    public bool Assumed { get; init; }

    public bool Synthetic { get; init; }

    public int CheckInCutoffMin { get; init; }

    /// <summary>Service id text; identifies the timetable a delay history belongs to. Empty for flexible legs.</summary>
    public string ServiceKey { get; init; } = string.Empty;

    public string ProviderId { get; init; } = string.Empty;

    /// <summary>Identifier of this concrete run, when it has one (provider run id or a kept booking leg id).</summary>
    public string? RunKey { get; init; }

    public static Hop FromRun(SnapshotRun r, long dep, long arr) => new()
    {
        Kind = HopKind.Scheduled,
        Mode = r.Mode,
        From = r.From,
        To = r.To,
        Dep = dep,
        Arr = arr,
        Fare = r.Fare,
        Pricing = r.Pricing,
        Run = r,
        Synthetic = r.Synthetic,
        CheckInCutoffMin = r.CheckInCutoffMin,
        ServiceKey = r.ServiceId.Value,
        ProviderId = r.ProviderId,
        RunKey = r.ServiceRunId,
    };

    public bool IsScheduled => Kind == HopKind.Scheduled;

    public int DurationMin => (int)(Arr - Dep);

    /// <summary>Stable text identifying this hop, used for deterministic ids and signatures.</summary>
    public string Key => RunKey ?? $"{Mode}:{From}>{To}@{Dep}";

    public Hop Retimed(long dep, long arr) => new()
    {
        Kind = Kind,
        Mode = Mode,
        From = From,
        To = To,
        Dep = dep,
        Arr = arr,
        Fare = Fare,
        Pricing = Pricing,
        Run = Run,
        Assumed = Assumed,
        Synthetic = Synthetic,
        CheckInCutoffMin = CheckInCutoffMin,
        ServiceKey = ServiceKey,
        ProviderId = ProviderId,
        RunKey = RunKey,
    };
}

internal sealed record FlexLink(PlaceId From, PlaceId To, TransportMode Mode, int DurationMin, Money Fare, PricingKind Pricing, bool Assumed);

internal static class TimeMath
{
    public static long Minute(DateTimeOffset instant) => Math.DivRem(instant.ToUnixTimeSeconds(), 60L, out _);

    public static long MinuteCeil(DateTimeOffset instant)
    {
        var seconds = instant.ToUnixTimeSeconds();
        return seconds / 60 + (seconds % 60 == 0 ? 0 : 1);
    }

    public static DateTimeOffset Instant(long minute) => DateTimeOffset.FromUnixTimeSeconds(minute * 60);

    public static int LocalMinuteOfDay(long minute)
    {
        var local = minute + (long)Dhathuru.Domain.Time.MaldivesTime.Offset.TotalMinutes;
        return (int)(((local % 1440) + 1440) % 1440);
    }

    public static DateOnly LocalDate(long minute) =>
        Dhathuru.Domain.Time.MaldivesTime.LocalDate(Instant(minute));
}
