using Dhathuru.Catalog.Data;
using Dhathuru.DataTools.Commands;

namespace Dhathuru.DataTools.Tests;

public sealed class Workspace : IDisposable
{
    public Workspace()
    {
        Root = Path.Combine(Path.GetTempPath(), "kandu-tests-" + Guid.NewGuid().ToString("N"));
        Datasets = Path.Combine(Root, "datasets");
        Seed = Path.Combine(Root, "seed");
        Cache = Path.Combine(Root, "cache");
        Directory.CreateDirectory(Path.Combine(Datasets, "Avas_Ride"));
        Directory.CreateDirectory(Path.Combine(Datasets, "FollowMe", "FollowMe_august_2025"));
        Directory.CreateDirectory(Path.Combine(Datasets, "MACL"));
        Directory.CreateDirectory(Seed);
        // the real catalog is the zone source for the taxi builder
        File.Copy(Path.Combine(RepoSeed(), SeedFiles.CatalogFile), Path.Combine(Seed, SeedFiles.CatalogFile));
    }

    public string Root { get; }

    public string Datasets { get; }

    public string Seed { get; }

    public string Cache { get; }

    public static string RepoSeed()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "apps", "api", "seed");
            if (File.Exists(Path.Combine(candidate, SeedFiles.CatalogFile))) return candidate;
        }
        throw new DirectoryNotFoundException("apps/api/seed not found");
    }

    public async Task<(int Code, string Out, string Err)> RunAsync(params string[] args)
    {
        var o = new StringWriter();
        var e = new StringWriter();
        var full = args.Concat(["--datasets", Datasets, "--seed", Seed, "--cache", Cache]).ToArray();
        var code = await DataToolsCli.RunAsync(full, o, e, TestContext.Current.CancellationToken);
        return (code, o.ToString(), e.ToString());
    }

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); }
        catch (IOException) { /* temp folder; the OS cleans up */ }
    }
}

public class CliTests
{
    private const string FareMatrix =
        "city,from_zone \\ to_zone,Male,Hulhumale Phase 1,Velana International Airport,Hulhumale Phase 2,Sea Plane,Domestic Terminal\n"
        + "Male City,Male,30.0,85.0,70.0,100.0,110.0,80.0\n"
        + "Male City,Hulhumale Phase 1,85.0,30.0,80.0,40.0,110.0,90.0\n"
        + "Male City,Velana International Airport,70.0,80.0,30.0,85.0,60.0,30.0\n"
        + "Male City,Hulhumale Phase 2,100.0,40.0,85.0,30.0,120.0,95.0\n"
        + "Male City,Sea Plane,110.0,110.0,60.0,120.0,30.0,70.0\n"
        + "Male City,Domestic Terminal,80.0,90.0,30.0,95.0,70.0,30.0\n";

    private static string RidesCsv() =>
        "date_started,pickup_lat,pickup_lng,dropoff_lat,dropoff_lng,distance_km,time_sec,status\n"
        + string.Join("\n", Enumerable.Range(0, 40).Select(i =>
            string.Create(System.Globalization.CultureInfo.InvariantCulture, $"2026-08-01 0{i % 9}:10:00,4.1755,73.5093,4.2170,73.5400,{8 + i * 0.01},{780 + i},payment_completed")));

    [Fact]
    public async Task Help_and_unknown_commands_are_handled()
    {
        using var ws = new Workspace();
        Assert.Equal(0, (await ws.RunAsync("help")).Code);
        var unknown = await ws.RunAsync("frobnicate");
        Assert.Equal(2, unknown.Code);
        Assert.Contains("Unknown command", unknown.Err);
        Assert.Equal(2, (await DataToolsCli.RunAsync([], new StringWriter(), new StringWriter(), TestContext.Current.CancellationToken)));
    }

    [Fact]
    public async Task Validate_seed_accepts_the_real_seed_files()
    {
        var o = new StringWriter();
        var code = await DataToolsCli.RunAsync(["validate-seed", "--seed", Workspace.RepoSeed()], o, new StringWriter(), TestContext.Current.CancellationToken);
        Assert.Equal(0, code);
        Assert.Contains("seed ok", o.ToString());
    }

    [Fact]
    public async Task Validate_seed_reports_a_broken_seed_with_exit_code_1()
    {
        using var ws = new Workspace();
        File.WriteAllText(Path.Combine(ws.Seed, SeedFiles.CatalogFile), File.ReadAllText(Path.Combine(ws.Seed, SeedFiles.CatalogFile)).Replace("\"schemaVersion\": 1", "\"schemaVersion\": 2", StringComparison.Ordinal));
        var result = await ws.RunAsync("validate-seed");
        Assert.Equal(1, result.Code);
        Assert.StartsWith("error:", result.Err);
    }

    [Fact]
    public async Task Build_taxi_model_writes_the_seed_and_a_rerun_is_byte_identical()
    {
        using var ws = new Workspace();
        File.WriteAllText(Path.Combine(ws.Datasets, "Avas_Ride", "avas_rides_aug_sep_2026.csv"), RidesCsv());
        File.WriteAllText(Path.Combine(ws.Datasets, "Avas_Ride", "car_fare_matrix.csv"), FareMatrix);

        var first = await ws.RunAsync("build-taxi-model");
        Assert.Equal(0, first.Code);
        var path = Path.Combine(ws.Seed, SeedFiles.TaxiModelFile);
        var bytes = File.ReadAllBytes(path);
        var again = await ws.RunAsync("build-taxi-model");
        Assert.Equal(0, again.Code);
        Assert.Equal(bytes, File.ReadAllBytes(path));
        Assert.Contains("taxi-model:", first.Out);

        // the written file loads through the catalog's own validating loader
        var data = SeedFiles.Load(ws.Seed);
        Assert.Equal(36, data.Taxi!.Fares.Count);
        Assert.NotEmpty(data.Taxi.Durations);
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact]
    public async Task Missing_dataset_is_a_clear_error_not_a_crash()
    {
        using var ws = new Workspace();
        var result = await ws.RunAsync("build-taxi-model");
        Assert.NotEqual(0, result.Code);
    }

    [Fact]
    public async Task Sea_speeds_resume_from_the_per_file_cache_and_give_the_same_answer()
    {
        using var ws = new Workspace();
        var dir = Path.Combine(ws.Datasets, "FollowMe", "FollowMe_august_2025");
        const string header = "poll_id,imei,timestamp,device_date,latitude,longitude,speed,heading,network,cellid,gsm_level\n";
        for (var f = 1; f <= 2; f++)
        {
            File.WriteAllText(Path.Combine(dir, $"{f}.csv"), header + string.Join("\n",
                Enumerable.Range(0, 12).SelectMany(d => Enumerable.Range(0, 5).Select(i =>
                    $"{d}{i},35985708553{f}{d:D3},\"2025-08-0{f} 09:00:0{i}\",\"2025-08-0{f} 09:00:0{i}\",4.2054,73.5011,{8 + i},0,1,1,1"))));
        }

        var first = await ws.RunAsync("build-sea-speeds");
        Assert.Equal(0, first.Code);
        Assert.Contains("scanning 1.csv", first.Out);
        var output = Path.Combine(ws.Seed, "sea-speeds.json");
        var bytes = File.ReadAllBytes(output);

        var second = await ws.RunAsync("build-sea-speeds");
        Assert.Contains("cached   1.csv", second.Out);
        Assert.DoesNotContain("scanning", second.Out);
        Assert.Equal(bytes, File.ReadAllBytes(output));
        Assert.Contains("2 files", second.Out);
    }

    [Fact]
    public async Task A_corrupt_cache_entry_is_rescanned_instead_of_trusted()
    {
        using var ws = new Workspace();
        var dir = Path.Combine(ws.Datasets, "FollowMe", "FollowMe_august_2025");
        File.WriteAllText(Path.Combine(dir, "1.csv"), "poll_id,imei,timestamp,device_date,latitude,longitude,speed,heading,network,cellid,gsm_level\n1,1,\"x\",\"2025-08-01 09:00:00\",4.2,73.5,5,0,1,1,1\n");
        await ws.RunAsync("build-sea-speeds");
        foreach (var f in Directory.GetFiles(ws.Cache)) File.WriteAllText(f, "{ not json");
        var result = await ws.RunAsync("build-sea-speeds");
        Assert.Equal(0, result.Code);
        Assert.Contains("scanning 1.csv", result.Out);
    }

    [Fact]
    public async Task Air_seasonality_writes_a_small_file()
    {
        using var ws = new Workspace();
        File.WriteAllText(Path.Combine(ws.Datasets, "MACL", "Airport traffic_Dataset_27092026.csv"),
            "record_id,flight_type,dep_date,arrival_date,carrier_code,route_code,region,aircraft_type_code,aircraft_category,arr_adults,arr_children,arr_infants,transit_adults,transit_children,transit_infants,dep_adults,dep_children,dep_infants,ata,atd\n"
            + "1,SCHEDULED,3/3/2024,3/3/2024,Carrier_001,Route_0001,Europe,Type_001,Widebody,100,0,0,0,0,0,100,0,0,10:30,14:05\n");
        var result = await ws.RunAsync("build-air-seasonality");
        Assert.Equal(0, result.Code);
        Assert.True(new FileInfo(Path.Combine(ws.Seed, "air-seasonality.json")).Length < 5_000);
    }
}
