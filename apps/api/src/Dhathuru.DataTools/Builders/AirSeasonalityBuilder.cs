using System.Globalization;
using Dhathuru.DataTools.Csv;

namespace Dhathuru.DataTools.Builders;

public sealed class AirSeasonalityOutput
{
    public int SchemaVersion { get; set; } = 1;

    public string Notice { get; set; } = "";

    public int[] YearsUsed { get; set; } = [];

    /// <summary>Index 0..11 = January..December. 1.0 is an average month.</summary>
    public double[] MonthIndex { get; set; } = [];

    /// <summary>Share of passenger movements by hour of day (Maldives local), arrivals and departures.</summary>
    public double[] ArrivalHourShare { get; set; } = [];

    public double[] DepartureHourShare { get; set; } = [];

    public Dictionary<string, long> AnnualPassengers { get; set; } = [];
}

/// <summary>International airport traffic aggregated to month-of-year and hour-of-day. Contains no carrier or route codes.</summary>
public static class AirSeasonalityBuilder
{
    public static AirSeasonalityOutput Build(CsvStream csv, IReadOnlyCollection<int> fullYears)
    {
        var dep = csv.Index("dep_date");
        var cols = new[] { "arr_adults", "arr_children", "arr_infants", "dep_adults", "dep_children", "dep_infants" }.Select(csv.Index).ToArray();
        var ata = csv.Index("ata");
        var atd = csv.Index("atd");

        var annual = new Dictionary<int, long>();
        var monthTotals = new long[12];
        var arrHours = new long[24];
        var depHours = new long[24];
        while (csv.Next() is { } row)
        {
            if (!DateTime.TryParseExact(row[dep], "M/d/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) continue;
            long arrivals = 0, departures = 0;
            for (var i = 0; i < 3; i++) arrivals += Number(row[cols[i]]);
            for (var i = 3; i < 6; i++) departures += Number(row[cols[i]]);
            annual[date.Year] = annual.GetValueOrDefault(date.Year) + arrivals + departures;
            if (fullYears.Contains(date.Year)) monthTotals[date.Month - 1] += arrivals + departures;
            AddHour(arrHours, row[ata], arrivals);
            AddHour(depHours, row[atd], departures);
        }

        var meanMonth = monthTotals.Average();
        return new AirSeasonalityOutput
        {
            Notice = "International movements only (domestic flights are not in the source). Airline and route identifiers are dropped. "
                     + "Used as a crowding prior, not as a timetable. Hour of day is the recorded time as given (assumed Maldives local).",
            YearsUsed = [.. fullYears.OrderBy(y => y)],
            MonthIndex = [.. monthTotals.Select(m => meanMonth == 0 ? 1 : Math.Round(m / meanMonth, 3))],
            ArrivalHourShare = Share(arrHours),
            DepartureHourShare = Share(depHours),
            AnnualPassengers = annual.OrderBy(a => a.Key).ToDictionary(a => a.Key.ToString(CultureInfo.InvariantCulture), a => a.Value),
        };
    }

    private static long Number(string value) => long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) ? n : 0;

    private static void AddHour(long[] hours, string time, long weight)
    {
        var colon = time.IndexOf(':', StringComparison.Ordinal);
        if (colon > 0 && int.TryParse(time.AsSpan(0, colon), NumberStyles.None, CultureInfo.InvariantCulture, out var h) && h is >= 0 and <= 23)
            hours[h] += weight;
    }

    private static double[] Share(long[] hours)
    {
        var total = hours.Sum();
        return [.. hours.Select(h => total == 0 ? 0 : Math.Round((double)h / total, 4))];
    }
}
