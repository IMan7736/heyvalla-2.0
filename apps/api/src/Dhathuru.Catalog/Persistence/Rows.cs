using NetTopologySuite.Geometries;

namespace Dhathuru.Catalog.Persistence;

public sealed class PlaceRow
{
    public string Id { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Type { get; set; } = "";
    public string NamesJson { get; set; } = "{}";
    public Point Location { get; set; } = null!;
    public string? AtollId { get; set; }
    public string? ParentId { get; set; }
    public string? Code { get; set; }
    public bool IsHub { get; set; }
    public bool Approximate { get; set; }
    public string Source { get; set; } = "";
    public string? ZoneKey { get; set; }
}

public sealed class OperatorRow
{
    public string Id { get; set; } = "";
    public string Slug { get; set; } = "";
    public string NamesJson { get; set; } = "{}";
    public string[] Modes { get; set; } = [];
    public string ProviderId { get; set; } = "";
    public string ProviderKind { get; set; } = "";
    public string? Website { get; set; }
}

public sealed class ServiceRow
{
    public string Id { get; set; } = "";
    public string Slug { get; set; } = "";
    public string OperatorId { get; set; } = "";
    public string Mode { get; set; } = "";
    public string? FromId { get; set; }
    public string? ToId { get; set; }
    public int DurationMin { get; set; }
    public long FareMinor { get; set; }
    public string Pricing { get; set; } = "";
    public int CheckInCutoffMin { get; set; }
    public bool CutoffVerified { get; set; }
    public int[] DaysOfWeek { get; set; } = [];
    public string[] Times { get; set; } = [];
    public string? FrequencyFirst { get; set; }
    public string? FrequencyLast { get; set; }
    public int? FrequencyHeadwayMin { get; set; }
    public DateOnly ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }
    public bool Synthetic { get; set; }
    public bool OnDemand { get; set; }
    public int? ChildDiscountPercent { get; set; }
}

public sealed class MinTransferRow
{
    public string PlaceId { get; set; } = "";
    public string FromMode { get; set; } = "";
    public string ToMode { get; set; } = "";
    public int Minutes { get; set; }
    public bool Verified { get; set; }
}

public sealed class LinkRow
{
    public string FromId { get; set; } = "";
    public string ToId { get; set; } = "";
    public string Mode { get; set; } = "";
    public int DurationMin { get; set; }
    public long FareMinor { get; set; }
    public string Pricing { get; set; } = "";
    public bool Approximate { get; set; }
}

public sealed class ReliabilityPriorRow
{
    public string Mode { get; set; } = "";
    public double OnTimeRate { get; set; }
    public double P90DelayMin { get; set; }
    public int Samples { get; set; }
    public bool Synthetic { get; set; }
}

public sealed class TaxiFareRow
{
    public string FromZone { get; set; } = "";
    public string ToZone { get; set; } = "";
    public long FareMinor { get; set; }
}

public sealed class TaxiDurationRow
{
    public string FromZone { get; set; } = "";
    public string ToZone { get; set; } = "";
    public string Band { get; set; } = "";
    public int Samples { get; set; }
    public double MedianMin { get; set; }
    public double P90Min { get; set; }
    public double MedianKm { get; set; }
}

/// <summary>Small key/value store for model parameters (fallback fits, time bands) and the imported data version.</summary>
public sealed class ModelParamRow
{
    public string Key { get; set; } = "";
    public string JsonValue { get; set; } = "null";
}
