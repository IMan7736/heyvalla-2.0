using System.Globalization;
using System.Text.Json;
using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Places;
using Dhathuru.Domain.Pricing;
using Dhathuru.Domain.Transport;

namespace Dhathuru.Catalog.Data;

/// <summary>Thrown when a seed file is malformed or inconsistent. The message names the offending record.</summary>
public sealed class SeedValidationException(string message) : Exception(message);

/// <summary>Reads <c>apps/api/seed/catalog.json</c> and <c>taxi-model.json</c> into <see cref="CatalogData"/>, validating everything.</summary>
public static class SeedFiles
{
    public const string CatalogFile = "catalog.json";
    public const string TaxiModelFile = "taxi-model.json";

    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        WriteIndented = true,
    };

    public static CatalogData Load(string seedDirectory)
    {
        var catalogPath = Path.Combine(seedDirectory, CatalogFile);
        var taxiPath = Path.Combine(seedDirectory, TaxiModelFile);
        return Parse(File.ReadAllText(catalogPath), File.Exists(taxiPath) ? File.ReadAllText(taxiPath) : null);
    }

    public static CatalogData Parse(string catalogJson, string? taxiJson = null)
    {
        var doc = JsonSerializer.Deserialize<SeedCatalog>(catalogJson, Json) ?? throw new SeedValidationException("catalog json is empty.");
        var taxi = taxiJson is null
            ? null
            : ParseTaxi(JsonSerializer.Deserialize<SeedTaxiModel>(taxiJson, Json) ?? throw new SeedValidationException("taxi json is empty."));
        return Build(doc, taxi, VersionOf(catalogJson, taxiJson));
    }

    private static string VersionOf(string catalogJson, string? taxiJson) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(catalogJson + "\n" + taxiJson)))[..16].ToLowerInvariant();

    private static CatalogData Build(SeedCatalog doc, TaxiModel? taxi, string version)
    {
        if (doc.SchemaVersion != 1) throw new SeedValidationException($"Unsupported catalog schemaVersion {doc.SchemaVersion}.");

        var places = doc.Places.Select(ToPlace).ToList();
        EnsureUnique(places.Select(p => p.Id.Value), "place id");
        EnsureUnique(places.Select(p => p.Slug), "place slug");
        var placeIds = places.Select(p => p.Id).ToHashSet();
        foreach (var p in places)
        {
            if (p.AtollId is { } a && !placeIds.Contains(a)) throw new SeedValidationException($"Place {p.Slug}: atollId {a} not found.");
            if (p.ParentId is { } par && !placeIds.Contains(par)) throw new SeedValidationException($"Place {p.Slug}: parentId {par} not found.");
        }

        var operators = doc.Operators.Select(ToOperator).ToList();
        EnsureUnique(operators.Select(o => o.Id.Value), "operator id");
        var operatorIds = operators.Select(o => o.Id).ToHashSet();

        var services = doc.Services.Select(ToService).ToList();
        EnsureUnique(services.Select(s => s.Id.Value), "service id");
        foreach (var s in services)
        {
            if (!operatorIds.Contains(s.OperatorId)) throw new SeedValidationException($"Service {s.Slug}: operator not found.");
            if (s.From is { } f && !placeIds.Contains(f)) throw new SeedValidationException($"Service {s.Slug}: from {f} not found.");
            if (s.To is { } t && !placeIds.Contains(t)) throw new SeedValidationException($"Service {s.Slug}: to {t} not found.");
            if (!s.OnDemand && (s.From is null || s.To is null)) throw new SeedValidationException($"Service {s.Slug}: scheduled services need from and to.");
            if (!s.OnDemand && s.Times.Count == 0 && s.Frequency is null) throw new SeedValidationException($"Service {s.Slug}: no departure times.");
            if (!s.OnDemand && s.DurationMin <= 0) throw new SeedValidationException($"Service {s.Slug}: durationMin must be positive.");
            if (s.DaysOfWeek.Any(d => d is < 1 or > 7)) throw new SeedValidationException($"Service {s.Slug}: daysOfWeek must be 1..7.");
        }

        var minTransfers = doc.MinTransfers.Select(m => new MinTransferRule(
            PlaceId.Parse(m.PlaceId), WireNames.ParseMode(m.FromMode), WireNames.ParseMode(m.ToMode), m.Minutes, m.Verified)).ToList();
        foreach (var m in minTransfers)
        {
            if (!placeIds.Contains(m.PlaceId)) throw new SeedValidationException($"MinTransfer: place {m.PlaceId} not found.");
            if (m.Minutes < 0) throw new SeedValidationException("MinTransfer: minutes must not be negative.");
        }

        var links = doc.Links.Select(l => new PlaceLink(
            PlaceId.Parse(l.FromId), PlaceId.Parse(l.ToId), WireNames.ParseMode(l.Mode), l.DurationMin,
            new Money(l.FareMinor), WireNames.ParsePricing(l.Pricing), l.Approximate)).ToList();
        foreach (var l in links)
        {
            if (!placeIds.Contains(l.From) || !placeIds.Contains(l.To)) throw new SeedValidationException($"Link {l.From} to {l.To}: place not found.");
        }

        var priors = doc.ReliabilityPriors.Select(p => new ReliabilityPrior(WireNames.ParseMode(p.Mode), p.OnTimeRate, p.P90DelayMin, p.Samples, p.Synthetic)).ToList();
        foreach (var p in priors)
        {
            if (p.OnTimeRate is < 0 or > 1) throw new SeedValidationException($"Reliability prior {p.Mode}: onTimeRate must be 0..1.");
        }

        if (taxi is not null)
        {
            var zones = places.Where(p => p.ZoneKey is not null).Select(p => p.ZoneKey!).ToHashSet();
            foreach (var z in taxi.Fares.SelectMany(f => new[] { f.FromZone, f.ToZone }).Concat(taxi.Durations.SelectMany(d => new[] { d.FromZone, d.ToZone })).Distinct())
            {
                if (!zones.Contains(z)) throw new SeedValidationException($"Taxi model references zone '{z}' that no catalog place declares as zoneKey.");
            }
        }

        return new CatalogData(places, operators, services, minTransfers, links, priors, taxi, version);
    }

    private static CatalogPlace ToPlace(SeedPlace p)
    {
        var type = WireNames.ParsePlaceType(p.Type);
        var id = PlaceId.Parse(p.Id);
        if (id.Type != type) throw new SeedValidationException($"Place {p.Slug}: id prefix does not match type {p.Type}.");
        if (!p.Name.ContainsKey("en")) throw new SeedValidationException($"Place {p.Slug}: name.en is required.");
        return new CatalogPlace(
            id, p.Slug, type, p.Name, new GeoPoint(p.Lat, p.Lng),
            p.AtollId is null ? null : PlaceId.Parse(p.AtollId), p.ParentId is null ? null : PlaceId.Parse(p.ParentId),
            p.Code, p.IsHub, p.Approximate, p.Source, p.ZoneKey);
    }

    private static CatalogOperator ToOperator(SeedOperator o) => new(
        OperatorId.Parse(o.Id), o.Slug, o.Name, o.Modes.Select(WireNames.ParseMode).ToList(), o.ProviderId,
        WireNames.ParseProviderKind(o.ProviderKind), o.Website);

    private static CatalogService ToService(SeedService s) => new(
        ServiceId.Parse(s.Id), s.Slug, OperatorId.Parse(s.OperatorId), WireNames.ParseMode(s.Mode),
        s.FromId is null ? null : PlaceId.Parse(s.FromId), s.ToId is null ? null : PlaceId.Parse(s.ToId),
        s.DurationMin, new Money(s.FareMinor), WireNames.ParsePricing(s.Pricing), s.CheckInCutoffMin, s.CutoffVerified,
        s.DaysOfWeek, (s.Times ?? []).Select(ParseTime).ToList(),
        s.Frequency is null ? null : new Frequency(ParseTime(s.Frequency.First), ParseTime(s.Frequency.Last), s.Frequency.HeadwayMin),
        DateOnly.Parse(s.ValidFrom, CultureInfo.InvariantCulture), s.ValidTo is null ? null : DateOnly.Parse(s.ValidTo, CultureInfo.InvariantCulture),
        s.Synthetic, s.OnDemand, s.ChildDiscountPercent);

    private static TimeOnly ParseTime(string value) =>
        TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t)
            ? t
            : throw new SeedValidationException($"Time '{value}' is not HH:mm.");

    private static void EnsureUnique(IEnumerable<string> values, string what)
    {
        var dup = values.GroupBy(v => v).FirstOrDefault(g => g.Count() > 1);
        if (dup is not null) throw new SeedValidationException($"Duplicate {what}: {dup.Key}");
    }

    public static TaxiModel ParseTaxi(SeedTaxiModel m)
    {
        if (m.SchemaVersion != 1) throw new SeedValidationException($"Unsupported taxi schemaVersion {m.SchemaVersion}.");
        var fares = m.Fares.Select(f => new TaxiFare(f.From, f.To, f.FareMinor)).ToList();
        foreach (var f in fares)
        {
            var back = fares.FirstOrDefault(x => x.FromZone == f.ToZone && x.ToZone == f.FromZone);
            if (back is not null && back.FareMinor != f.FareMinor)
                throw new SeedValidationException($"Taxi fare matrix is not symmetric for {f.FromZone}/{f.ToZone}.");
        }
        var durations = m.Durations.Select(d => new TaxiDurationStat(d.From, d.To, d.Band, d.Samples, d.MedianMin, d.P90Min, d.MedianKm)).ToList();
        if (durations.Any(d => d.Samples < TaxiModel.MinSamples))
            throw new SeedValidationException($"Taxi durations below the minimum group size of {TaxiModel.MinSamples} must not be published.");
        return new TaxiModel(
            fares, durations,
            new LinearFit(m.DurationFallback.Intercept, m.DurationFallback.PerKm, m.DurationFallback.R2, m.DurationFallback.Samples),
            new LinearFit(m.FareFallback.Intercept, m.FareFallback.PerKm, m.FareFallback.R2, m.FareFallback.Samples),
            m.Bands.ToDictionary(b => b.Key, b => (b.Value.From, b.Value.To)));
    }
}

#pragma warning disable CS1591
public sealed class SeedCatalog
{
    public int SchemaVersion { get; set; }
    public string? Notice { get; set; }
    public List<SeedPlace> Places { get; set; } = [];
    public List<SeedOperator> Operators { get; set; } = [];
    public List<SeedService> Services { get; set; } = [];
    public List<SeedMinTransfer> MinTransfers { get; set; } = [];
    public List<SeedLink> Links { get; set; } = [];
    public List<SeedPrior> ReliabilityPriors { get; set; } = [];
}

public sealed class SeedPlace
{
    public string Id { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Type { get; set; } = "";
    public Dictionary<string, string> Name { get; set; } = [];
    public double Lat { get; set; }
    public double Lng { get; set; }
    public string? AtollId { get; set; }
    public string? ParentId { get; set; }
    public string? Code { get; set; }
    public bool IsHub { get; set; }
    public bool Approximate { get; set; }
    public string Source { get; set; } = "";
    public string? ZoneKey { get; set; }
}

public sealed class SeedOperator
{
    public string Id { get; set; } = "";
    public string Slug { get; set; } = "";
    public Dictionary<string, string> Name { get; set; } = [];
    public List<string> Modes { get; set; } = [];
    public string ProviderId { get; set; } = "";
    public string ProviderKind { get; set; } = "mock";
    public string? Website { get; set; }
}

public sealed class SeedFrequency
{
    public string First { get; set; } = "";
    public string Last { get; set; } = "";
    public int HeadwayMin { get; set; }
}

public sealed class SeedService
{
    public string Id { get; set; } = "";
    public string Slug { get; set; } = "";
    public string OperatorId { get; set; } = "";
    public string Mode { get; set; } = "";
    public string? FromId { get; set; }
    public string? ToId { get; set; }
    public int DurationMin { get; set; }
    public long FareMinor { get; set; }
    public string Pricing { get; set; } = "fixed";
    public int CheckInCutoffMin { get; set; }
    public bool CutoffVerified { get; set; }
    public List<int> DaysOfWeek { get; set; } = [];
    public List<string>? Times { get; set; }
    public SeedFrequency? Frequency { get; set; }
    public string ValidFrom { get; set; } = "";
    public string? ValidTo { get; set; }
    public bool Synthetic { get; set; }
    public bool OnDemand { get; set; }
    public int? ChildDiscountPercent { get; set; }
}

public sealed class SeedMinTransfer
{
    public string PlaceId { get; set; } = "";
    public string FromMode { get; set; } = "";
    public string ToMode { get; set; } = "";
    public int Minutes { get; set; }
    public bool Verified { get; set; }
}

public sealed class SeedLink
{
    public string FromId { get; set; } = "";
    public string ToId { get; set; } = "";
    public string Mode { get; set; } = "";
    public int DurationMin { get; set; }
    public long FareMinor { get; set; }
    public string Pricing { get; set; } = "fixed";
    public bool Approximate { get; set; }
}

public sealed class SeedPrior
{
    public string Mode { get; set; } = "";
    public double OnTimeRate { get; set; }
    public double P90DelayMin { get; set; }
    public int Samples { get; set; }
    public bool Synthetic { get; set; }
}

public sealed class SeedTaxiModel
{
    public int SchemaVersion { get; set; }
    public string? Notice { get; set; }
    public List<SeedTaxiFare> Fares { get; set; } = [];
    public List<SeedTaxiDuration> Durations { get; set; } = [];
    public SeedFit DurationFallback { get; set; } = new();
    public SeedFit FareFallback { get; set; } = new();
    public Dictionary<string, SeedBand> Bands { get; set; } = [];
}

public sealed class SeedTaxiFare
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public long FareMinor { get; set; }
}

public sealed class SeedTaxiDuration
{
    public string From { get; set; } = "";
    public string To { get; set; } = "";
    public string Band { get; set; } = "";
    public int Samples { get; set; }
    public double MedianMin { get; set; }
    public double P90Min { get; set; }
    public double MedianKm { get; set; }
}

public sealed class SeedFit
{
    public double Intercept { get; set; }
    public double PerKm { get; set; }
    public double R2 { get; set; }
    public int Samples { get; set; }
}

public sealed class SeedBand
{
    public int From { get; set; }
    public int To { get; set; }
}
#pragma warning restore CS1591
