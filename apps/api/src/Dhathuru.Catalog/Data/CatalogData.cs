using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Places;
using Dhathuru.Domain.Pricing;
using Dhathuru.Domain.Providers;
using Dhathuru.Domain.Transport;

namespace Dhathuru.Catalog.Data;

/// <summary>Localised text: locale tag to text, <c>en</c> always present.</summary>
public sealed record CatalogPlace(
    PlaceId Id,
    string Slug,
    PlaceType Type,
    IReadOnlyDictionary<string, string> Name,
    GeoPoint Geo,
    PlaceId? AtollId,
    PlaceId? ParentId,
    string? Code,
    bool IsHub,
    bool Approximate,
    string Source,
    string? ZoneKey);

public sealed record CatalogOperator(
    OperatorId Id,
    string Slug,
    IReadOnlyDictionary<string, string> Name,
    IReadOnlyList<TransportMode> Modes,
    string ProviderId,
    ProviderKind ProviderKind,
    string? Website);

public sealed record Frequency(TimeOnly First, TimeOnly Last, int HeadwayMin);

/// <summary>
/// A run pattern: fixed local times and/or a frequency window, on given ISO weekdays between two dates.
/// <see cref="Synthetic"/> and <see cref="CutoffVerified"/> say how much of this is a guess.
/// </summary>
public sealed record CatalogService(
    ServiceId Id,
    string Slug,
    OperatorId OperatorId,
    TransportMode Mode,
    PlaceId? From,
    PlaceId? To,
    int DurationMin,
    Money Fare,
    PricingKind Pricing,
    int CheckInCutoffMin,
    bool CutoffVerified,
    IReadOnlyList<int> DaysOfWeek,
    IReadOnlyList<TimeOnly> Times,
    Frequency? Frequency,
    DateOnly ValidFrom,
    DateOnly? ValidTo,
    bool Synthetic,
    bool OnDemand,
    int? ChildDiscountPercent);

/// <summary>Minimum minutes needed to change from one mode to another at a place (unverified until an operator confirms).</summary>
public sealed record MinTransferRule(PlaceId PlaceId, TransportMode FromMode, TransportMode ToMode, int Minutes, bool Verified);

/// <summary>Walking or road link between two places usable between legs.</summary>
public sealed record PlaceLink(PlaceId From, PlaceId To, TransportMode Mode, int DurationMin, Money Fare, PricingKind Pricing, bool Approximate);

/// <summary>Synthetic unless <c>Samples &gt; 0</c>. Samples == 0 means "prior, no observations".</summary>
public sealed record ReliabilityPrior(TransportMode Mode, double OnTimeRate, double P90DelayMin, int Samples, bool Synthetic);

public sealed record CatalogData(
    IReadOnlyList<CatalogPlace> Places,
    IReadOnlyList<CatalogOperator> Operators,
    IReadOnlyList<CatalogService> Services,
    IReadOnlyList<MinTransferRule> MinTransfers,
    IReadOnlyList<PlaceLink> Links,
    IReadOnlyList<ReliabilityPrior> ReliabilityPriors,
    TaxiModel? Taxi,
    string Version = "empty")
{
    public static CatalogData Empty { get; } = new([], [], [], [], [], [], null);
}
