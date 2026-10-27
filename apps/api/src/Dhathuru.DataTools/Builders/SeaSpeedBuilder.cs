using System.Globalization;
using System.Text.Json;
using Dhathuru.DataTools.Csv;

namespace Dhathuru.DataTools.Builders;

/// <summary>Per grid cell accumulation. Devices are kept as hashes, capped, only to prove "at least N distinct devices".</summary>
public sealed class SeaCell
{
    public const int SpeedBins = 61; // 0..59 plus a >= 60 bin is not used: faster pings are dropped as glitches
    public const int DeviceCap = 32;

    public int Pings { get; set; }

    public int[] Hist { get; set; } = new int[SpeedBins];

    public List<int> Devices { get; set; } = [];

    public void Merge(SeaCell other)
    {
        Pings += other.Pings;
        for (var i = 0; i < Hist.Length; i++) Hist[i] += other.Hist[i];
        foreach (var d in other.Devices)
        {
            if (Devices.Count >= DeviceCap) break;
            if (!Devices.Contains(d)) Devices.Add(d);
        }
    }
}

/// <summary>Result of scanning one FollowMe file. Cached on disk so a long run can resume.</summary>
public sealed class SeaPartial
{
    public Dictionary<string, SeaCell> Cells { get; set; } = [];

    public int[] HourlyPings { get; set; } = new int[24];

    public int Rows { get; set; }

    public int Dropped { get; set; }
}

public sealed record SeaSpeedCell(double Lat, double Lng, int Devices, int MovingPings, double P50, double P90);

public sealed class SeaSpeedOutput
{
    public int SchemaVersion { get; set; } = 1;

    public string Notice { get; set; } = "";

    public double CellSizeDeg { get; set; }

    public int MinDevices { get; set; }

    public string SpeedUnit { get; set; } = "unverified (assumed knots)";

    public int FilesProcessed { get; set; }

    public long Rows { get; set; }

    public double[] HourlyShareUtc { get; set; } = [];

    public double GlobalMovingP50 { get; set; }

    public double GlobalMovingP90 { get; set; }

    public List<SeaSpeedCell> Cells { get; set; } = [];
}

/// <summary>
/// Aggregates vessel GPS pings into 0.01 degree cells (about 1.1 km). Drops device id, cell id and exact positions;
/// publishes a cell only if at least <see cref="MinDevices"/> distinct devices were seen in it.
/// </summary>
public static class SeaSpeedBuilder
{
    public const double CellSizeDeg = 0.01;
    public const int MinDevices = 10;
    public const int MinMovingPings = 30;
    public const double MovingThreshold = 0.5;
    public const double MaxPlausibleSpeed = 60;

    public static SeaPartial ScanFile(CsvStream csv)
    {
        var imei = csv.Index("imei");
        var date = csv.Index("device_date");
        var lat = csv.Index("latitude");
        var lng = csv.Index("longitude");
        var speed = csv.Index("speed");
        var partial = new SeaPartial();
        while (csv.Next() is { } row)
        {
            partial.Rows++;
            if (!double.TryParse(row[lat], NumberStyles.Float, CultureInfo.InvariantCulture, out var la)
                || !double.TryParse(row[lng], NumberStyles.Float, CultureInfo.InvariantCulture, out var lo)
                || !double.TryParse(row[speed], NumberStyles.Float, CultureInfo.InvariantCulture, out var sp)
                || (la == 0 && lo == 0) || sp < 0 || sp > MaxPlausibleSpeed)
            {
                partial.Dropped++;
                continue;
            }
            if (row[date].Length >= 13 && int.TryParse(row[date].AsSpan(11, 2), NumberStyles.None, CultureInfo.InvariantCulture, out var h) && h is >= 0 and <= 23)
                partial.HourlyPings[h]++;

            var key = CellKey(la, lo);
            if (!partial.Cells.TryGetValue(key, out var cell)) partial.Cells[key] = cell = new SeaCell();
            cell.Pings++;
            if (sp >= MovingThreshold) cell.Hist[(int)Math.Min(sp, SeaCell.SpeedBins - 1)]++;
            if (cell.Devices.Count < SeaCell.DeviceCap)
            {
                var hash = StableHash(row[imei]);
                if (!cell.Devices.Contains(hash)) cell.Devices.Add(hash);
            }
        }
        return partial;
    }

    public static SeaSpeedOutput Combine(IEnumerable<SeaPartial> partials)
    {
        var cells = new Dictionary<string, SeaCell>();
        var hourly = new long[24];
        long rows = 0;
        var files = 0;
        foreach (var p in partials)
        {
            files++;
            rows += p.Rows;
            for (var h = 0; h < 24; h++) hourly[h] += p.HourlyPings[h];
            foreach (var (key, cell) in p.Cells)
            {
                if (!cells.TryGetValue(key, out var existing)) cells[key] = existing = new SeaCell();
                existing.Merge(cell);
            }
        }

        var global = new long[SeaCell.SpeedBins];
        var output = new SeaSpeedOutput
        {
            Notice = "Aggregated vessel GPS pings (FollowMe, Aug 2025). Cells have at least 10 distinct devices. No device ids, no timestamps below hour of day. "
                     + "Speed unit is not stated by the source (assumed knots). Mixed vessel types. Illustrative only, never navigational.",
            CellSizeDeg = CellSizeDeg, MinDevices = MinDevices, FilesProcessed = files, Rows = rows,
        };
        foreach (var (key, cell) in cells.OrderBy(c => c.Key, StringComparer.Ordinal))
        {
            var moving = cell.Hist.Sum();
            var cellHist = cell.Hist.Select(c => (long)c).ToArray();
            for (var i = 0; i < global.Length; i++) global[i] += cell.Hist[i];
            if (cell.Devices.Count < MinDevices || moving < MinMovingPings) continue;
            var (la, lo) = ParseKey(key);
            output.Cells.Add(new SeaSpeedCell(la, lo, cell.Devices.Count, moving, HistPercentile(cellHist, 0.5), HistPercentile(cellHist, 0.9)));
        }
        var total = hourly.Sum();
        output.HourlyShareUtc = [.. hourly.Select(h => total == 0 ? 0 : Math.Round((double)h / total, 4))];
        output.GlobalMovingP50 = HistPercentile(global, 0.5);
        output.GlobalMovingP90 = HistPercentile(global, 0.9);
        return output;
    }

    internal static string CellKey(double lat, double lng) =>
        string.Create(CultureInfo.InvariantCulture, $"{Math.Floor(lat / CellSizeDeg):0}:{Math.Floor(lng / CellSizeDeg):0}");

    internal static (double Lat, double Lng) ParseKey(string key)
    {
        var parts = key.Split(':');
        // cell centre, rounded to the grid so no sub-cell position leaks
        return (Math.Round((long.Parse(parts[0], CultureInfo.InvariantCulture) + 0.5) * CellSizeDeg, 3),
                Math.Round((long.Parse(parts[1], CultureInfo.InvariantCulture) + 0.5) * CellSizeDeg, 3));
    }

    internal static double HistPercentile(IReadOnlyList<long> hist, double p)
    {
        long total = 0;
        foreach (var c in hist) total += c;
        if (total == 0) return 0;
        var target = p * total;
        long acc = 0;
        for (var i = 0; i < hist.Count; i++)
        {
            acc += hist[i];
            if (acc >= target) return i;
        }
        return hist.Count - 1;
    }

    private static int StableHash(string value)
    {
        unchecked
        {
            var h = (int)2166136261;
            foreach (var c in value) h = (h ^ c) * 16777619;
            return h;
        }
    }

    public static string SerializePartial(SeaPartial p) => JsonSerializer.Serialize(p);

    public static SeaPartial? DeserializePartial(string json) => JsonSerializer.Deserialize<SeaPartial>(json);
}
