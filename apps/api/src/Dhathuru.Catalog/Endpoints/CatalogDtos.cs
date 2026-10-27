using System.Text.Json.Serialization;
using Dhathuru.Catalog.Data;
using Dhathuru.Domain.Pricing;

namespace Dhathuru.Catalog.Endpoints;

public sealed record GeoDto(double Lat, double Lng);

public sealed record MoneyDto(long AmountMinor, string Currency)
{
    public static MoneyDto From(Money m) => new(m.AmountMinor, m.Currency);
}

public sealed record PlaceDto(
    string Id,
    string Type,
    IReadOnlyDictionary<string, string> Name,
    GeoDto Geo,
    bool IsHub,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? AtollId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ParentId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Code,
    bool Approximate)
{
    public static PlaceDto From(CatalogPlace p) => new(
        p.Id.Value, WireNames.ToWire(p.Type), p.Name, new GeoDto(p.Geo.Lat, p.Geo.Lng), p.IsHub,
        p.AtollId?.Value, p.ParentId?.Value, p.Code, p.Approximate);
}

public sealed record PlaceRefDto(
    string Id,
    string Type,
    IReadOnlyDictionary<string, string> Name,
    GeoDto Geo,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Code)
{
    public static PlaceRefDto From(CatalogPlace p) => new(p.Id.Value, WireNames.ToWire(p.Type), p.Name, new GeoDto(p.Geo.Lat, p.Geo.Lng), p.Code);
}

public sealed record PageInfoDto(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? NextCursor,
    int Limit);

public sealed record PlacePageDto(IReadOnlyList<PlaceDto> Items, PageInfoDto Page);

public sealed record OperatorDto(
    string Id,
    IReadOnlyDictionary<string, string> Name,
    IReadOnlyList<string> Modes,
    string ProviderId,
    string ProviderKind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Website)
{
    public static OperatorDto From(CatalogOperator o) => new(
        o.Id.Value, o.Name, [.. o.Modes.Select(WireNames.ToWire)], o.ProviderId, WireNames.ToWire(o.ProviderKind), o.Website);
}

public sealed record OperatorListDto(IReadOnlyList<OperatorDto> Items);

public sealed record DepartureDto(string ServiceRunId, DateTimeOffset DepartAt, DateTimeOffset ArriveAt);

public sealed record ValidityDto(
    string From,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? To,
    IReadOnlyList<int> DaysOfWeek);

public sealed record FareDto(
    MoneyDto Base,
    string Pricing,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? ChildDiscountPercent);

/// <summary>Service as in the OpenAPI contract; <c>synthetic</c> is true when the schedule is not operator published.</summary>
public sealed record ServiceDto(
    string Id,
    string OperatorId,
    string Mode,
    string Kind,
    PlaceRefDto From,
    PlaceRefDto To,
    IReadOnlyList<DepartureDto> Departures,
    ValidityDto Validity,
    FareDto Fare,
    int CheckInCutoffMin,
    bool Synthetic);

public sealed record ServiceListDto(IReadOnlyList<ServiceDto> Items);
