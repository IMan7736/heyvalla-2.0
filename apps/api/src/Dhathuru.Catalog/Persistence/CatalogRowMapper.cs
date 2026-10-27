using System.Globalization;
using System.Text.Json;
using Dhathuru.Catalog.Data;
using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Places;
using Dhathuru.Domain.Pricing;
using NetTopologySuite.Geometries;

namespace Dhathuru.Catalog.Persistence;

/// <summary>Converts between the in-memory <see cref="CatalogData"/> and database rows.</summary>
public static class CatalogRowMapper
{
    public const int Srid = 4326;
    public const string VersionKey = "data.version";
    public const string DurationFallbackKey = "taxi.durationFallback";
    public const string FareFallbackKey = "taxi.fareFallback";
    public const string BandsKey = "taxi.bands";

    private static readonly GeometryFactory Factory = new(new PrecisionModel(), Srid);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static PlaceRow ToRow(CatalogPlace p) => new()
    {
        Id = p.Id.Value, Slug = p.Slug, Type = WireNames.ToWire(p.Type), NamesJson = JsonSerializer.Serialize(p.Name, Json),
        Location = Factory.CreatePoint(new Coordinate(p.Geo.Lng, p.Geo.Lat)), AtollId = p.AtollId?.Value, ParentId = p.ParentId?.Value,
        Code = p.Code, IsHub = p.IsHub, Approximate = p.Approximate, Source = p.Source, ZoneKey = p.ZoneKey,
    };

    public static CatalogPlace FromRow(PlaceRow r) => new(
        PlaceId.Parse(r.Id), r.Slug, WireNames.ParsePlaceType(r.Type),
        JsonSerializer.Deserialize<Dictionary<string, string>>(r.NamesJson, Json) ?? [],
        new GeoPoint(r.Location.Y, r.Location.X), r.AtollId is null ? null : PlaceId.Parse(r.AtollId),
        r.ParentId is null ? null : PlaceId.Parse(r.ParentId), r.Code, r.IsHub, r.Approximate, r.Source, r.ZoneKey);

    public static OperatorRow ToRow(CatalogOperator o) => new()
    {
        Id = o.Id.Value, Slug = o.Slug, NamesJson = JsonSerializer.Serialize(o.Name, Json), Modes = [.. o.Modes.Select(WireNames.ToWire)],
        ProviderId = o.ProviderId, ProviderKind = WireNames.ToWire(o.ProviderKind), Website = o.Website,
    };

    public static CatalogOperator FromRow(OperatorRow r) => new(
        OperatorId.Parse(r.Id), r.Slug, JsonSerializer.Deserialize<Dictionary<string, string>>(r.NamesJson, Json) ?? [],
        [.. r.Modes.Select(WireNames.ParseMode)], r.ProviderId, WireNames.ParseProviderKind(r.ProviderKind), r.Website);

    public static ServiceRow ToRow(CatalogService s) => new()
    {
        Id = s.Id.Value, Slug = s.Slug, OperatorId = s.OperatorId.Value, Mode = WireNames.ToWire(s.Mode),
        FromId = s.From?.Value, ToId = s.To?.Value, DurationMin = s.DurationMin, FareMinor = s.Fare.AmountMinor,
        Pricing = WireNames.ToWire(s.Pricing), CheckInCutoffMin = s.CheckInCutoffMin, CutoffVerified = s.CutoffVerified,
        DaysOfWeek = [.. s.DaysOfWeek], Times = [.. s.Times.Select(t => t.ToString("HH:mm", CultureInfo.InvariantCulture))],
        FrequencyFirst = s.Frequency?.First.ToString("HH:mm", CultureInfo.InvariantCulture),
        FrequencyLast = s.Frequency?.Last.ToString("HH:mm", CultureInfo.InvariantCulture),
        FrequencyHeadwayMin = s.Frequency?.HeadwayMin, ValidFrom = s.ValidFrom, ValidTo = s.ValidTo,
        Synthetic = s.Synthetic, OnDemand = s.OnDemand, ChildDiscountPercent = s.ChildDiscountPercent,
    };

    public static CatalogService FromRow(ServiceRow r) => new(
        ServiceId.Parse(r.Id), r.Slug, OperatorId.Parse(r.OperatorId), WireNames.ParseMode(r.Mode),
        r.FromId is null ? null : PlaceId.Parse(r.FromId), r.ToId is null ? null : PlaceId.Parse(r.ToId),
        r.DurationMin, new Money(r.FareMinor), WireNames.ParsePricing(r.Pricing), r.CheckInCutoffMin, r.CutoffVerified,
        r.DaysOfWeek, [.. r.Times.Select(ParseTime)],
        r.FrequencyFirst is null || r.FrequencyLast is null || r.FrequencyHeadwayMin is null
            ? null
            : new Frequency(ParseTime(r.FrequencyFirst), ParseTime(r.FrequencyLast), r.FrequencyHeadwayMin.Value),
        r.ValidFrom, r.ValidTo, r.Synthetic, r.OnDemand, r.ChildDiscountPercent);

    public static MinTransferRow ToRow(MinTransferRule m) => new()
    {
        PlaceId = m.PlaceId.Value, FromMode = WireNames.ToWire(m.FromMode), ToMode = WireNames.ToWire(m.ToMode), Minutes = m.Minutes, Verified = m.Verified,
    };

    public static MinTransferRule FromRow(MinTransferRow r) =>
        new(PlaceId.Parse(r.PlaceId), WireNames.ParseMode(r.FromMode), WireNames.ParseMode(r.ToMode), r.Minutes, r.Verified);

    public static LinkRow ToRow(PlaceLink l) => new()
    {
        FromId = l.From.Value, ToId = l.To.Value, Mode = WireNames.ToWire(l.Mode), DurationMin = l.DurationMin,
        FareMinor = l.Fare.AmountMinor, Pricing = WireNames.ToWire(l.Pricing), Approximate = l.Approximate,
    };

    public static PlaceLink FromRow(LinkRow r) => new(
        PlaceId.Parse(r.FromId), PlaceId.Parse(r.ToId), WireNames.ParseMode(r.Mode), r.DurationMin,
        new Money(r.FareMinor), WireNames.ParsePricing(r.Pricing), r.Approximate);

    public static ReliabilityPriorRow ToRow(ReliabilityPrior p) => new()
    {
        Mode = WireNames.ToWire(p.Mode), OnTimeRate = p.OnTimeRate, P90DelayMin = p.P90DelayMin, Samples = p.Samples, Synthetic = p.Synthetic,
    };

    public static ReliabilityPrior FromRow(ReliabilityPriorRow r) =>
        new(WireNames.ParseMode(r.Mode), r.OnTimeRate, r.P90DelayMin, r.Samples, r.Synthetic);

    public static IEnumerable<ModelParamRow> ModelParamsOf(CatalogData data)
    {
        yield return new ModelParamRow { Key = VersionKey, JsonValue = JsonSerializer.Serialize(data.Version, Json) };
        if (data.Taxi is { } t)
        {
            yield return new ModelParamRow { Key = DurationFallbackKey, JsonValue = JsonSerializer.Serialize(t.DurationFallbackMin, Json) };
            yield return new ModelParamRow { Key = FareFallbackKey, JsonValue = JsonSerializer.Serialize(t.FareFallbackMvr, Json) };
            yield return new ModelParamRow
            {
                Key = BandsKey,
                JsonValue = JsonSerializer.Serialize(t.Bands.ToDictionary(b => b.Key, b => new SeedBand { From = b.Value.FromHour, To = b.Value.ToHour }), Json),
            };
        }
    }

    public static TaxiModel? TaxiFromRows(
        IReadOnlyList<TaxiFareRow> fares, IReadOnlyList<TaxiDurationRow> durations, IReadOnlyDictionary<string, string> modelParams)
    {
        if (!modelParams.TryGetValue(DurationFallbackKey, out var dur) || !modelParams.TryGetValue(FareFallbackKey, out var fare)
            || !modelParams.TryGetValue(BandsKey, out var bands))
            return null;
        return new TaxiModel(
            [.. fares.Select(f => new TaxiFare(f.FromZone, f.ToZone, f.FareMinor))],
            [.. durations.Select(d => new TaxiDurationStat(d.FromZone, d.ToZone, d.Band, d.Samples, d.MedianMin, d.P90Min, d.MedianKm))],
            JsonSerializer.Deserialize<LinearFit>(dur, Json)!, JsonSerializer.Deserialize<LinearFit>(fare, Json)!,
            JsonSerializer.Deserialize<Dictionary<string, SeedBand>>(bands, Json)!.ToDictionary(b => b.Key, b => (b.Value.From, b.Value.To)));
    }

    private static TimeOnly ParseTime(string value) => TimeOnly.ParseExact(value, "HH:mm", CultureInfo.InvariantCulture);
}
