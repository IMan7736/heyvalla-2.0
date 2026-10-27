using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Places;
using Dhathuru.Domain.Pricing;
using Dhathuru.Domain.Transport;

namespace Dhathuru.Domain.Trips;

public enum TimeMode { DepartAfter, ArriveBy }

public enum RiskTolerance { Cautious, Balanced, Relaxed }

public enum RiskLevel { Low, Medium, High }

public enum TripLabel { Fastest, Cheapest, Safest, BestOverall }

public enum Severity { Info, Good, Caution }

public sealed record Passengers(int Adults, int Children = 0, int Infants = 0)
{
    public int Total => Adults + Children + Infants;
}

public sealed record TripPreferences(
    RiskTolerance RiskTolerance,
    int? MaxTransfers = null,
    Money? BudgetMax = null,
    IReadOnlyList<TransportMode>? Modes = null);

/// <summary><c>Date</c> and <c>Time</c> are Indian/Maldives local; convert with <see cref="Time.MaldivesTime"/>.</summary>
public sealed record TripSearchQuery(
    PlaceId From,
    PlaceId To,
    DateOnly Date,
    TimeOnly Time,
    TimeMode TimeMode,
    Passengers Passengers,
    TripPreferences Preferences);

public sealed record Leg(
    LegId Id,
    TransportMode Mode,
    string ProviderId,
    PlaceRef From,
    PlaceRef To,
    DateTimeOffset DepartAt,
    DateTimeOffset ArriveAt,
    Money Price,
    bool Bookable,
    PricingKind Pricing,
    IReadOnlyList<string> NoteCodes,
    ServiceId? ServiceId = null,
    string? ServiceRunId = null,
    string? GeometryRef = null,
    string? OperatorName = null)
{
    public int DurationMin => (int)(ArriveAt - DepartAt).TotalMinutes;
}

public sealed record Buffer(int AfterLegIndex, int Minutes, int RequiredMinutes, int ComfortMinutes);

public sealed record Risk(double MissProbability, RiskLevel Level, int? WeakestConnectionIndex = null);

/// <summary>Structured reason. The API returns codes and parameters only; the web owns the English text.</summary>
public sealed record Explanation(string Code, IReadOnlyDictionary<string, object?> Params, Severity Severity);

public sealed record TripWarning(string Code, IReadOnlyDictionary<string, object?> Params);

public sealed record PriceBreakdownItem(string Code, Money Amount, LegId? LegId = null);

public sealed record TripPrice(Money Total, IReadOnlyList<PriceBreakdownItem> Breakdown);

public sealed record TripOption(
    TripOptionId Id,
    DateTimeOffset DepartAt,
    DateTimeOffset ArriveAt,
    int Transfers,
    IReadOnlyList<Leg> Legs,
    TripPrice Price,
    IReadOnlyList<Buffer> Buffers,
    Risk Risk,
    IReadOnlyList<TripLabel> Labels,
    double Score,
    IReadOnlyList<Explanation> Explanations,
    IReadOnlyList<TripWarning> Warnings)
{
    public int DurationMin => (int)(ArriveAt - DepartAt).TotalMinutes;
}

public sealed record TripSearchResult(SearchId SearchId, IReadOnlyList<TripOption> Options, IReadOnlyList<Explanation>? Notices = null)
{
    /// <summary>Codes explaining gaps (no route, closed taxis, unknown place). Never null.</summary>
    public IReadOnlyList<Explanation> NoticeList => Notices ?? [];
}

public enum DisruptionKind { Delayed, Cancelled }

public sealed record Disruption(LegId LegId, DisruptionKind Kind, int? DelayMinutes = null);

public sealed record ReplanQuery(BookingId BookingId, DateTimeOffset Now, Disruption Disruption);
