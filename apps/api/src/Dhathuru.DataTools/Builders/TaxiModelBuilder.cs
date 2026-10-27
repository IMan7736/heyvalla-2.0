using System.Globalization;
using Dhathuru.Catalog.Data;
using Dhathuru.Catalog.Services;
using Dhathuru.DataTools.Csv;
using Dhathuru.Domain.Places;

namespace Dhathuru.DataTools.Builders;

public sealed record ZoneCentroid(string Key, GeoPoint Geo);

public sealed record TaxiBuildReport(int RidesRead, int RidesUsed, int RidesOutsideZones, int Groups, int GroupsPublished, int GroupsSuppressed);

/// <summary>
/// Builds <c>taxi-model.json</c> from the ride log and the zone fare matrix.
/// Privacy: rides are streamed once; only per (zone, zone, time band) aggregates with n &gt;= 10 are kept; coordinates and
/// timestamps are never written, only zone keys and rounded durations.
/// </summary>
public static class TaxiModelBuilder
{
    /// <summary>Maximum distance from a zone centroid for a ride end to count as inside that zone.</summary>
    public const double ZoneRadiusKm = 3.0;

    private const double MinTimeSecForFit = 301; // time_sec is clipped at 300 s; clipped rides would bias the fit

    private static readonly Dictionary<string, string> MatrixZoneKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Male"] = "male",
        ["Hulhumale Phase 1"] = "hulhumale-1",
        ["Velana International Airport"] = "velana",
        ["Hulhumale Phase 2"] = "hulhumale-2",
        ["Sea Plane"] = "seaplane",
        ["Domestic Terminal"] = "domestic-terminal",
    };

    public static readonly IReadOnlyDictionary<string, (int From, int To)> Bands = new Dictionary<string, (int, int)>
    {
        ["night"] = (0, 5),
        ["morning"] = (6, 10),
        ["midday"] = (11, 15),
        ["evening"] = (16, 23),
    };

    /// <summary>Reads the 6x6 matrix (MVR whole numbers) into minor-unit fares. Fails if it is not symmetric.</summary>
    public static List<SeedTaxiFare> ReadFareMatrix(CsvStream csv)
    {
        var fromCol = csv.Index("from_zone \\ to_zone");
        var header = csv.Columns.ToList();
        var fares = new List<SeedTaxiFare>();
        var matrixColumns = header.Where(h => MatrixZoneKeys.ContainsKey(h)).ToList();
        while (csv.Next() is { } row)
        {
            if (!MatrixZoneKeys.TryGetValue(row[fromCol].Trim(), out var from))
                throw new InvalidDataException($"Unknown fare matrix zone '{row[fromCol]}'.");
            foreach (var col in matrixColumns)
            {
                var value = double.Parse(row[csv.Index(col)], CultureInfo.InvariantCulture);
                if (value < 0 || value != Math.Floor(value)) throw new InvalidDataException($"Fare '{value}' is not a whole MVR amount.");
                fares.Add(new SeedTaxiFare { From = from, To = MatrixZoneKeys[col], FareMinor = (long)value * 100 });
            }
        }
        foreach (var f in fares)
        {
            var back = fares.FirstOrDefault(x => x.From == f.To && x.To == f.From);
            if (back is null || back.FareMinor != f.FareMinor)
                throw new InvalidDataException($"Fare matrix is not symmetric for {f.From}/{f.To}.");
        }
        return [.. fares.OrderBy(f => f.From, StringComparer.Ordinal).ThenBy(f => f.To, StringComparer.Ordinal)];
    }

    public static (SeedTaxiModel Model, TaxiBuildReport Report) Build(CsvStream rides, IReadOnlyList<SeedTaxiFare> fares, IReadOnlyList<ZoneCentroid> zones)
    {
        var started = rides.Index("date_started");
        var plat = rides.Index("pickup_lat");
        var plng = rides.Index("pickup_lng");
        var dlat = rides.Index("dropoff_lat");
        var dlng = rides.Index("dropoff_lng");
        var km = rides.Index("distance_km");
        var sec = rides.Index("time_sec");
        var status = rides.Index("status");

        var groups = new Dictionary<(string From, string To, string Band), (List<double> Minutes, List<double> Km)>();
        var durationFit = new Stats.LinearAccumulator();
        int read = 0, used = 0, outside = 0;

        while (rides.Next() is { } row)
        {
            read++;
            if (row[status] is not ("payment_completed" or "ride_completed")) continue; // expired rides have no real duration
            if (!double.TryParse(row[sec], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
                || !double.TryParse(row[km], NumberStyles.Float, CultureInfo.InvariantCulture, out var distance)
                || !TryHour(row[started], out var hour)
                || !TryPoint(row[plat], row[plng], out var pickup)
                || !TryPoint(row[dlat], row[dlng], out var dropoff))
                continue;

            if (seconds >= MinTimeSecForFit) durationFit.Add(distance, seconds / 60.0);

            var from = Nearest(pickup, zones);
            var to = Nearest(dropoff, zones);
            if (from is null || to is null)
            {
                outside++;
                continue;
            }
            used++;
            var band = BandOf(hour);
            var key = (from, to, band);
            if (!groups.TryGetValue(key, out var g)) groups[key] = g = ([], []);
            g.Minutes.Add(seconds / 60.0);
            g.Km.Add(distance);
        }

        var durations = new List<SeedTaxiDuration>();
        var suppressed = 0;
        foreach (var ((from, to, band), g) in groups.OrderBy(k => k.Key.From, StringComparer.Ordinal).ThenBy(k => k.Key.To, StringComparer.Ordinal).ThenBy(k => k.Key.Band, StringComparer.Ordinal))
        {
            if (g.Minutes.Count < TaxiModel.MinSamples)
            {
                suppressed++;
                continue;
            }
            durations.Add(new SeedTaxiDuration
            {
                From = from, To = to, Band = band, Samples = g.Minutes.Count,
                MedianMin = Math.Round(Stats.Percentile(g.Minutes, 0.5), 1),
                P90Min = Math.Round(Stats.Percentile(g.Minutes, 0.9), 1),
                MedianKm = Math.Round(Stats.Percentile(g.Km, 0.5), 1),
            });
        }

        var dFit = durationFit.Fit();
        var centroids = zones.ToDictionary(z => z.Key, z => z.Geo);
        var farePoints = fares.Where(f => f.From != f.To && centroids.ContainsKey(f.From) && centroids.ContainsKey(f.To))
            .Select(f => (X: GeoMath.HaversineKm(centroids[f.From], centroids[f.To]) * TaxiModel.DefaultDetourFactor, Y: f.FareMinor / 100.0))
            .ToList();
        var fFit = Stats.LinearFit(farePoints);

        var model = new SeedTaxiModel
        {
            SchemaVersion = 1,
            Notice = "Fares: operator zone matrix (real). Durations: aggregated completed rides, groups of at least 10, zone keys only. "
                     + "Fallback fits are least squares guesses; see r2 and samples. Zone assignment uses nearest centroid within "
                     + $"{ZoneRadiusKm:0.#} km; the three airport-island zones are only about 1 km apart so their separation is approximate.",
            Fares = [.. fares],
            Durations = durations,
            DurationFallback = new SeedFit { Intercept = Math.Round(dFit.Intercept, 2), PerKm = Math.Round(dFit.Slope, 3), R2 = Math.Round(dFit.R2, 3), Samples = durationFit.Count },
            FareFallback = new SeedFit { Intercept = Math.Round(fFit.Intercept, 2), PerKm = Math.Round(fFit.Slope, 3), R2 = Math.Round(fFit.R2, 3), Samples = farePoints.Count },
            Bands = Bands.ToDictionary(b => b.Key, b => new SeedBand { From = b.Value.From, To = b.Value.To }),
        };
        return (model, new TaxiBuildReport(read, used, outside, groups.Count, durations.Count, suppressed));
    }

    internal static string BandOf(int hour) => Bands.First(b => hour >= b.Value.From && hour <= b.Value.To).Key;

    private static string? Nearest(GeoPoint p, IReadOnlyList<ZoneCentroid> zones)
    {
        string? best = null;
        var bestKm = double.MaxValue;
        foreach (var z in zones)
        {
            var d = GeoMath.HaversineKm(p, z.Geo);
            if (d < bestKm)
            {
                bestKm = d;
                best = z.Key;
            }
        }
        return bestKm <= ZoneRadiusKm ? best : null;
    }

    private static bool TryHour(string timestamp, out int hour)
    {
        hour = 0;
        return timestamp.Length >= 13 && int.TryParse(timestamp.AsSpan(11, 2), NumberStyles.None, CultureInfo.InvariantCulture, out hour) && hour is >= 0 and <= 23;
    }

    private static bool TryPoint(string lat, string lng, out GeoPoint point)
    {
        point = default;
        if (!double.TryParse(lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var la)
            || !double.TryParse(lng, NumberStyles.Float, CultureInfo.InvariantCulture, out var lo)
            || la is < -90 or > 90 || lo is < -180 or > 180)
            return false;
        point = new GeoPoint(la, lo);
        return true;
    }
}
