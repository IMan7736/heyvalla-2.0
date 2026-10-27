using System.Globalization;
using System.Text;
using System.Text.Json;
using Dhathuru.Catalog.Data;
using Dhathuru.DataTools.Builders;
using Dhathuru.DataTools.Csv;
using Dhathuru.Domain.Places;

namespace Dhathuru.DataTools.Tests;

public class CsvStreamTests
{
    private static CsvStream Csv(string text) => new(new StringReader(text));

    [Fact]
    public void Reads_quoted_fields_with_commas_quotes_and_newlines()
    {
        using var csv = Csv("a,b,c\n1,\"x,y\",\"say \"\"hi\"\"\"\n2,\"line1\nline2\",z\n");
        Assert.Equal(["1", "x,y", "say \"hi\""], csv.Next()!);
        Assert.Equal(["2", "line1\nline2", "z"], csv.Next()!);
        Assert.Null(csv.Next());
    }

    [Fact]
    public void Handles_crlf_blank_lines_missing_final_newline_and_empty_fields()
    {
        using var csv = Csv("a,b\r\n\r\n1,\r\n,2");
        Assert.Equal(["1", ""], csv.Next()!);
        Assert.Equal(["", "2"], csv.Next()!);
        Assert.Null(csv.Next());
    }

    [Fact]
    public void Header_lookup_ignores_case_and_a_byte_order_mark()
    {
        using var csv = Csv("﻿Record_Id,Name\n1,x");
        Assert.Equal(0, csv.Index("record_id"));
        Assert.Equal(1, csv.Index("NAME"));
        var ex = Assert.Throws<InvalidDataException>(() => csv.Index("missing"));
        Assert.Contains("missing", ex.Message);
    }

    [Fact]
    public void Empty_input_is_an_error_not_an_empty_table() => Assert.Throws<InvalidDataException>(() => Csv(""));

    [Fact]
    public void Streams_large_input_without_materialising_it()
    {
        var sb = new StringBuilder("a,b\n");
        for (var i = 0; i < 50_000; i++) sb.Append(i).Append(",x\n");
        using var csv = Csv(sb.ToString());
        var count = 0;
        while (csv.Next() is not null) count++;
        Assert.Equal(50_000, count);
    }
}

public class StatsTests
{
    [Fact]
    public void Percentile_interpolates()
    {
        Assert.Equal(2.5, Stats.Percentile([1, 2, 3, 4], 0.5));
        Assert.Equal(1, Stats.Percentile([4, 1, 3, 2], 0));
        Assert.Equal(4, Stats.Percentile([4, 1, 3, 2], 1));
        Assert.Equal(7, Stats.Percentile([7], 0.9));
        Assert.Throws<ArgumentException>(() => Stats.Percentile([], 0.5));
    }

    [Fact]
    public void Linear_fit_recovers_a_line_and_the_streaming_accumulator_agrees()
    {
        var points = Enumerable.Range(1, 20).Select(x => ((double)x, 3 + 2.5 * x)).ToList();
        var fit = Stats.LinearFit(points);
        Assert.Equal(3, fit.Intercept, 9);
        Assert.Equal(2.5, fit.Slope, 9);
        Assert.Equal(1, fit.R2, 9);

        var acc = new Stats.LinearAccumulator();
        foreach (var (x, y) in points) acc.Add(x, y);
        var streamed = acc.Fit();
        Assert.Equal(fit.Intercept, streamed.Intercept, 6);
        Assert.Equal(fit.Slope, streamed.Slope, 6);
    }

    [Fact]
    public void Noisy_data_has_a_low_r2()
    {
        var rng = new Random(1);
        var fit = Stats.LinearFit([.. Enumerable.Range(0, 200).Select(_ => (rng.NextDouble(), rng.NextDouble()))]);
        Assert.True(fit.R2 < 0.1);
    }

    [Fact]
    public void Degenerate_x_has_no_slope() => Assert.Equal(0, Stats.LinearFit([(1, 2), (1, 4)]).Slope);
}

public class TaxiModelBuilderTests
{
    private static readonly ZoneCentroid[] Zones =
    [
        new("male", new GeoPoint(4.1755, 73.5093)),
        new("hulhumale-1", new GeoPoint(4.2170, 73.5400)),
    ];

    private static CsvStream Rides(IEnumerable<string> rows) =>
        new(new StringReader("date_started,pickup_lat,pickup_lng,dropoff_lat,dropoff_lng,distance_km,time_sec,status\n" + string.Join("\n", rows)));

    private static string Ride(string hour, double lat, double lng, double dlat, double dlng, double km, double sec, string status = "payment_completed") =>
        string.Create(CultureInfo.InvariantCulture, $"2026-08-01 {hour}:15:00,{lat},{lng},{dlat},{dlng},{km},{sec},{status}");

    private static IEnumerable<string> MaleToHulhumale(int n, string hour = "09", double sec = 780, string status = "payment_completed") =>
        Enumerable.Range(0, n).Select(i => Ride(hour, 4.1755 + i * 1e-5, 73.5093, 4.2170, 73.5400 + i * 1e-5, 8.0 + i * 0.01, sec + i, status));

    private static List<SeedTaxiFare> Fares() =>
    [
        new() { From = "male", To = "hulhumale-1", FareMinor = 8500 }, new() { From = "hulhumale-1", To = "male", FareMinor = 8500 },
        new() { From = "male", To = "male", FareMinor = 3000 }, new() { From = "hulhumale-1", To = "hulhumale-1", FareMinor = 3000 },
    ];

    [Fact]
    public void Publishes_groups_of_ten_or_more_and_suppresses_smaller_ones()
    {
        var rows = MaleToHulhumale(25).Concat(MaleToHulhumale(9, hour: "20")).ToList(); // 9 rides in the evening band: suppressed
        var (model, report) = TaxiModelBuilder.Build(Rides(rows), Fares(), Zones);
        var group = Assert.Single(model.Durations);
        Assert.Equal(("male", "hulhumale-1", "morning", 25), (group.From, group.To, group.Band, group.Samples));
        Assert.Equal(1, report.GroupsSuppressed);
        Assert.Equal(34, report.RidesUsed);
        Assert.InRange(group.MedianMin, 13, 14);
        Assert.True(group.P90Min > group.MedianMin);
    }

    [Fact]
    public void Expired_rides_have_no_real_duration_and_are_ignored()
    {
        var rows = MaleToHulhumale(30, status: "ride_expired");
        var (model, report) = TaxiModelBuilder.Build(Rides(rows.Concat(MaleToHulhumale(12))), Fares(), Zones);
        Assert.Equal(12, Assert.Single(model.Durations).Samples);
        Assert.Equal(42, report.RidesRead);
    }

    [Fact]
    public void Output_carries_zone_keys_only_never_coordinates_or_timestamps()
    {
        var (model, _) = TaxiModelBuilder.Build(Rides(MaleToHulhumale(40)), Fares(), Zones);
        var json = JsonSerializer.Serialize(model);
        foreach (var leak in new[] { "4.1755", "73.5093", "4.2170", "2026-08-01", "pickup", "lat", "lng" })
            Assert.DoesNotContain(leak, json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rides_far_from_every_zone_are_dropped_not_misassigned()
    {
        var far = Enumerable.Range(0, 20).Select(_ => Ride("09", 6.0, 73.0, 4.2170, 73.5400, 80, 4000));
        var (model, report) = TaxiModelBuilder.Build(Rides(far.Concat(MaleToHulhumale(10))), Fares(), Zones);
        Assert.Equal(20, report.RidesOutsideZones);
        Assert.Equal(10, Assert.Single(model.Durations).Samples);
    }

    [Fact]
    public void Duration_fallback_is_fitted_on_unclipped_rides_only()
    {
        // 300 s is the source's floor; clipped rides would flatten the fit. Rides at the floor are excluded from it.
        var clipped = Enumerable.Range(0, 50).Select(i => Ride("09", 4.1755, 73.5093, 4.1756, 73.5094, 0.5 + i * 0.1, 300));
        var real = Enumerable.Range(1, 50).Select(i => Ride("09", 4.1755, 73.5093, 4.2170, 73.5400, i / 5.0, 60 * (2 + 1.5 * i / 5.0)));
        var (model, _) = TaxiModelBuilder.Build(Rides(clipped.Concat(real)), Fares(), Zones);
        Assert.Equal(40, model.DurationFallback.Samples); // the 10 real rides under 301 s are excluded too
        Assert.Equal(1.5, model.DurationFallback.PerKm, 3);
        Assert.Equal(2, model.DurationFallback.Intercept, 2);
        Assert.Equal(1, model.DurationFallback.R2, 3);
    }

    [Fact]
    public void Output_is_deterministic()
    {
        var rows = MaleToHulhumale(30).ToList();
        var a = JsonSerializer.Serialize(TaxiModelBuilder.Build(Rides(rows), Fares(), Zones).Model);
        var b = JsonSerializer.Serialize(TaxiModelBuilder.Build(Rides(rows), Fares(), Zones).Model);
        Assert.Equal(a, b);
    }

    [Fact]
    public void Fare_matrix_is_read_in_minor_units_and_validated()
    {
        const string header = "city,from_zone \\ to_zone,Male,Hulhumale Phase 1,Velana International Airport,Hulhumale Phase 2,Sea Plane,Domestic Terminal\n";
        var ok = header
                 + "Male City,Male,30.0,85.0,70.0,100.0,110.0,80.0\n"
                 + "Male City,Hulhumale Phase 1,85.0,30.0,80.0,40.0,110.0,90.0\n"
                 + "Male City,Velana International Airport,70.0,80.0,30.0,85.0,60.0,30.0\n"
                 + "Male City,Hulhumale Phase 2,100.0,40.0,85.0,30.0,120.0,95.0\n"
                 + "Male City,Sea Plane,110.0,110.0,60.0,120.0,30.0,70.0\n"
                 + "Male City,Domestic Terminal,80.0,90.0,30.0,95.0,70.0,30.0\n";
        var fares = TaxiModelBuilder.ReadFareMatrix(new CsvStream(new StringReader(ok)));
        Assert.Equal(36, fares.Count);
        Assert.Equal(8500, fares.Single(f => f.From == "male" && f.To == "hulhumale-1").FareMinor);

        var asymmetric = ok.Replace("Male City,Male,30.0,85.0", "Male City,Male,30.0,86.0", StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() => TaxiModelBuilder.ReadFareMatrix(new CsvStream(new StringReader(asymmetric))));
        var fractional = ok.Replace("85.0,30.0,80.0", "85.5,30.0,80.0", StringComparison.Ordinal);
        Assert.Throws<InvalidDataException>(() => TaxiModelBuilder.ReadFareMatrix(new CsvStream(new StringReader(fractional))));
        Assert.Throws<InvalidDataException>(() => TaxiModelBuilder.ReadFareMatrix(new CsvStream(new StringReader(ok.Replace("Domestic Terminal,", "Moon Base,", StringComparison.Ordinal)))));
    }

    [Fact]
    public void Built_model_passes_the_catalogs_own_validation()
    {
        var (model, _) = TaxiModelBuilder.Build(Rides(MaleToHulhumale(40)), Fares(), Zones);
        var taxi = SeedFiles.ParseTaxi(model);
        Assert.Single(taxi.Durations);
        Assert.Equal("morning", taxi.BandFor(9));
    }
}

public class SeaSpeedBuilderTests
{
    private const string Header = "poll_id,imei,timestamp,device_date,latitude,longitude,speed,heading,network,cellid,gsm_level\n";

    private static CsvStream Pings(IEnumerable<string> rows) => new(new StringReader(Header + string.Join("\n", rows)));

    private static string Ping(int device, double lat, double lng, double speed, string hour = "09") =>
        string.Create(CultureInfo.InvariantCulture, $"{device}00,3000000000{device:D3},\"2025-08-01 {hour}:00:05\",\"2025-08-01 {hour}:00:00\",{lat},{lng},{speed},90,1,60079,12");

    private static IEnumerable<string> Fleet(int devices, int pingsEach, double lat, double lng, double speed) =>
        Enumerable.Range(0, devices).SelectMany(d => Enumerable.Range(0, pingsEach).Select(i => Ping(d, lat + i * 1e-6, lng, speed + i % 3)));

    [Fact]
    public void A_cell_needs_ten_distinct_devices_to_be_published()
    {
        var thin = SeaSpeedBuilder.Combine([SeaSpeedBuilder.ScanFile(Pings(Fleet(9, 20, 4.2054, 73.5011, 10)))]);
        Assert.Empty(thin.Cells);
        var enough = SeaSpeedBuilder.Combine([SeaSpeedBuilder.ScanFile(Pings(Fleet(10, 20, 4.2054, 73.5011, 10)))]);
        var cell = Assert.Single(enough.Cells);
        Assert.Equal(10, cell.Devices);
        Assert.InRange(cell.P50, 10, 12);
    }

    [Fact]
    public void One_chatty_device_never_makes_a_cell_public()
    {
        var one = Enumerable.Range(0, 5000).Select(i => Ping(1, 4.2054, 73.5011, 8));
        Assert.Empty(SeaSpeedBuilder.Combine([SeaSpeedBuilder.ScanFile(Pings(one))]).Cells);
    }

    [Fact]
    public void Positions_are_snapped_to_the_grid_and_no_device_identity_survives()
    {
        var output = SeaSpeedBuilder.Combine([SeaSpeedBuilder.ScanFile(Pings(Fleet(12, 10, 4.20543, 73.50117, 9)))]);
        var cell = Assert.Single(output.Cells);
        Assert.Equal(4.205, cell.Lat, 6);
        Assert.Equal(73.505, cell.Lng, 6);
        var json = JsonSerializer.Serialize(output);
        foreach (var leak in new[] { "imei", "3000000000", "cellid", "60079", "poll", "2025-08-01" })
            Assert.DoesNotContain(leak, json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Glitch_speeds_zero_coordinates_and_garbage_are_dropped_and_counted()
    {
        var rows = new List<string> { Ping(1, 4.2, 73.5, 999), Ping(1, 0, 0, 5), Ping(1, 4.2, 73.5, -3), "1,2,\"x\",\"y\",abc,def,1,1,1,1,1" };
        var partial = SeaSpeedBuilder.ScanFile(Pings(rows));
        Assert.Equal(4, partial.Rows);
        Assert.Equal(4, partial.Dropped);
        Assert.Empty(partial.Cells);
    }

    [Fact]
    public void Stationary_pings_do_not_pull_the_moving_speed_down()
    {
        var rows = Fleet(10, 30, 4.2054, 73.5011, 12).Concat(Enumerable.Range(0, 10).SelectMany(d => Enumerable.Range(0, 100).Select(_ => Ping(d, 4.2054, 73.5011, 0))));
        var cell = Assert.Single(SeaSpeedBuilder.Combine([SeaSpeedBuilder.ScanFile(Pings(rows))]).Cells);
        Assert.InRange(cell.P50, 12, 14);
    }

    [Fact]
    public void Combining_cached_partials_equals_scanning_everything_at_once()
    {
        var a = Fleet(6, 30, 4.2054, 73.5011, 10).ToList();
        var b = Enumerable.Range(6, 6).SelectMany(d => Enumerable.Range(0, 30).Select(i => Ping(d, 4.2054, 73.5011, 10 + i % 3))).ToList();
        var split = SeaSpeedBuilder.Combine([
            SeaSpeedBuilder.DeserializePartial(SeaSpeedBuilder.SerializePartial(SeaSpeedBuilder.ScanFile(Pings(a))))!,
            SeaSpeedBuilder.DeserializePartial(SeaSpeedBuilder.SerializePartial(SeaSpeedBuilder.ScanFile(Pings(b))))!]);
        var whole = SeaSpeedBuilder.Combine([SeaSpeedBuilder.ScanFile(Pings(a.Concat(b)))]);
        Assert.Equal(JsonSerializer.Serialize(whole.Cells), JsonSerializer.Serialize(split.Cells));
        Assert.Equal(12, Assert.Single(split.Cells).Devices);
    }

    [Fact]
    public void Hourly_share_adds_up_to_one()
    {
        var rows = Fleet(10, 10, 4.2054, 73.5011, 5).Concat(Enumerable.Range(0, 100).Select(i => Ping(1, 4.2054, 73.5011, 5, "03")));
        var output = SeaSpeedBuilder.Combine([SeaSpeedBuilder.ScanFile(Pings(rows))]);
        Assert.Equal(1.0, output.HourlyShareUtc.Sum(), 2);
        Assert.True(output.HourlyShareUtc[3] > output.HourlyShareUtc[9] * 0.9);
    }
}

public class AirSeasonalityBuilderTests
{
    private const string Header = "record_id,flight_type,dep_date,arrival_date,carrier_code,route_code,region,aircraft_type_code,aircraft_category,arr_adults,arr_children,arr_infants,transit_adults,transit_children,transit_infants,dep_adults,dep_children,dep_infants,ata,atd\n";

    private static string Flight(int id, string date, int arr, int dep, string ata = "10:30", string atd = "14:05") =>
        $"{id},SCHEDULED,{date},{date},Carrier_001,Route_0001,Europe,Type_001,Widebody,{arr},0,0,0,0,0,{dep},0,0,{ata},{atd}";

    [Fact]
    public void Month_index_is_relative_to_the_average_month_and_ignores_other_years()
    {
        var rows = new List<string>();
        for (var month = 1; month <= 12; month++) rows.Add(Flight(month, $"{month}/15/2024", month == 12 ? 300 : 100, 0));
        rows.Add(Flight(99, "6/1/2020", 100000, 0)); // not a "full year" in the list: must not count
        using var csv = new CsvStream(new StringReader(Header + string.Join("\n", rows)));
        var output = AirSeasonalityBuilder.Build(csv, [2024]);
        Assert.Equal(12, output.MonthIndex.Length);
        Assert.Equal(12.0, output.MonthIndex.Sum(), 1);
        Assert.True(output.MonthIndex[11] > 2.5);
        Assert.Equal(1400, output.AnnualPassengers["2024"]);
        Assert.Equal(100000, output.AnnualPassengers["2020"]);
    }

    [Fact]
    public void Hours_are_shares_and_carrier_and_route_codes_never_appear()
    {
        var rows = new[] { Flight(1, "1/1/2024", 100, 100, "10:30", "14:05"), Flight(2, "1/2/2024", 100, 300, "10:50", "22:00") };
        using var csv = new CsvStream(new StringReader(Header + string.Join("\n", rows)));
        var output = AirSeasonalityBuilder.Build(csv, [2024]);
        Assert.Equal(1.0, output.ArrivalHourShare.Sum(), 3);
        Assert.Equal(1.0, output.DepartureHourShare.Sum(), 3);
        Assert.Equal(1.0, output.ArrivalHourShare[10], 3);
        Assert.Equal(0.25, output.DepartureHourShare[14], 3);
        var json = JsonSerializer.Serialize(output);
        Assert.DoesNotContain("Carrier", json, StringComparison.Ordinal);
        Assert.DoesNotContain("Route_", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Rows_with_missing_times_or_bad_dates_are_skipped_not_fatal()
    {
        var rows = new[] { Flight(1, "1/1/2024", 100, 100, "", ""), Flight(2, "not-a-date", 50, 50), Flight(3, "2/1/2024", 100, 100) };
        using var csv = new CsvStream(new StringReader(Header + string.Join("\n", rows)));
        var output = AirSeasonalityBuilder.Build(csv, [2024]);
        Assert.Equal(400, output.AnnualPassengers["2024"]);
    }
}
