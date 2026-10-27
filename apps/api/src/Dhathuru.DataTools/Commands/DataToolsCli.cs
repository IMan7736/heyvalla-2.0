using System.Globalization;
using System.Text.Json;
using Dhathuru.Catalog;
using Dhathuru.Catalog.Data;
using Dhathuru.Catalog.Persistence;
using Dhathuru.DataTools.Builders;
using Dhathuru.DataTools.Csv;
using Dhathuru.Domain.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Dhathuru.DataTools.Commands;

public static class DataToolsCli
{
    public const string RidesFile = "Avas_Ride/avas_rides_aug_sep_2026.csv";
    public const string FareMatrixFile = "Avas_Ride/car_fare_matrix.csv";
    public const string MaclFile = "MACL/Airport traffic_Dataset_27092026.csv";
    public const string FollowMeDir = "FollowMe/FollowMe_august_2025";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, CancellationToken ct)
    {
        if (args.Length == 0 || args[0] is "help" or "--help")
        {
            output.WriteLine(Usage);
            return args.Length == 0 ? 2 : 0;
        }
        try
        {
            var ctx = new CliContext(args.Skip(1), output, error);
            switch (args[0])
            {
                case "profile": return Profile(ctx);
                case "build-taxi-model": return BuildTaxiModel(ctx);
                case "build-sea-speeds": return BuildSeaSpeeds(ctx);
                case "build-air-seasonality": return BuildAirSeasonality(ctx);
                case "validate-seed": return ValidateSeed(ctx);
                case "import-catalog": return await ImportCatalogAsync(ctx, ct);
                default:
                    error.WriteLine($"Unknown command '{args[0]}'.\n{Usage}");
                    return 2;
            }
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or SeedValidationException or ArgumentException or InvalidOperationException or FormatException)
        {
            error.WriteLine($"error: {ex.Message}");
            return 1;
        }
    }

    public const string Usage = """
        Dhathuru.DataTools (KANDU). Reads datasets/ (read-only), writes small anonymised files to apps/api/seed/.
          profile                  print aggregate statistics of the CSV datasets (no rows)
          build-taxi-model         rides + fare matrix -> seed/taxi-model.json
          build-sea-speeds         FollowMe pings -> seed/sea-speeds.json (resumable; per-file cache)
          build-air-seasonality    MACL -> seed/air-seasonality.json
          validate-seed            load and validate seed/catalog.json and taxi-model.json
          import-catalog           migrate the catalog database and import the seed (idempotent)
        Options: --datasets <dir> --seed <dir> --cache <dir>
        Database for import-catalog: Database__Host/Port/Name/Username/Password environment variables.
        """;

    private static int Profile(CliContext ctx)
    {
        using (var rides = CsvStream.Open(Path.Combine(ctx.DatasetsDir, RidesFile)))
        {
            var status = rides.Index("status");
            var km = rides.Index("distance_km");
            var sec = rides.Index("time_sec");
            var started = rides.Index("date_started");
            var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
            var kms = new List<double>();
            var mins = new List<double>();
            string min = "9", max = "0";
            var n = 0;
            while (rides.Next() is { } row)
            {
                n++;
                counts[row[status]] = counts.GetValueOrDefault(row[status]) + 1;
                kms.Add(double.Parse(row[km], CultureInfo.InvariantCulture));
                mins.Add(double.Parse(row[sec], CultureInfo.InvariantCulture) / 60);
                if (string.CompareOrdinal(row[started], min) < 0) min = row[started];
                if (string.CompareOrdinal(row[started], max) > 0) max = row[started];
            }
            ctx.Output.WriteLine($"rides: {n} rows, {min[..10]}..{max[..10]}, status {string.Join(", ", counts.Select(c => $"{c.Key}={c.Value}"))}");
            ctx.Output.WriteLine($"  distance km p10/p50/p90: {Stats.Percentile(kms, 0.1):0.0}/{Stats.Percentile(kms, 0.5):0.0}/{Stats.Percentile(kms, 0.9):0.0}");
            ctx.Output.WriteLine($"  duration min p10/p50/p90: {Stats.Percentile(mins, 0.1):0.0}/{Stats.Percentile(mins, 0.5):0.0}/{Stats.Percentile(mins, 0.9):0.0}");
        }
        using (var macl = CsvStream.Open(Path.Combine(ctx.DatasetsDir, MaclFile)))
        {
            var dep = macl.Index("dep_date");
            var years = new SortedDictionary<int, int>();
            while (macl.Next() is { } row)
            {
                if (DateTime.TryParseExact(row[dep], "M/d/yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) years[d.Year] = years.GetValueOrDefault(d.Year) + 1;
            }
            ctx.Output.WriteLine($"macl: {years.Values.Sum()} movements by year: {string.Join(", ", years.Select(y => $"{y.Key}={y.Value}"))}");
        }
        var files = Directory.GetFiles(Path.Combine(ctx.DatasetsDir, FollowMeDir), "*.csv");
        ctx.Output.WriteLine($"followme: {files.Length} daily files, {files.Sum(f => new FileInfo(f).Length) / 1_000_000} MB (use build-sea-speeds for content statistics)");
        return 0;
    }

    private static int BuildTaxiModel(CliContext ctx)
    {
        var catalog = SeedFiles.Load(ctx.SeedDir);
        var zones = catalog.Places.Where(p => p.ZoneKey is not null).Select(p => new ZoneCentroid(p.ZoneKey!, p.Geo)).ToList();
        List<SeedTaxiFare> fares;
        using (var matrix = CsvStream.Open(Path.Combine(ctx.DatasetsDir, FareMatrixFile))) fares = TaxiModelBuilder.ReadFareMatrix(matrix);
        using var rides = CsvStream.Open(Path.Combine(ctx.DatasetsDir, RidesFile));
        var (model, report) = TaxiModelBuilder.Build(rides, fares, zones);
        var path = Path.Combine(ctx.SeedDir, SeedFiles.TaxiModelFile);
        WriteJson(path, model);
        ctx.Output.WriteLine($"taxi-model: {report.RidesRead} rides read, {report.RidesUsed} placed in zones, {report.RidesOutsideZones} outside zones; "
                             + $"{report.GroupsPublished}/{report.Groups} groups published (n>=10), {report.GroupsSuppressed} suppressed.");
        ctx.Output.WriteLine($"  duration fallback: {model.DurationFallback.Intercept} + {model.DurationFallback.PerKm}/km (R2 {model.DurationFallback.R2}, n={model.DurationFallback.Samples})");
        ctx.Output.WriteLine($"  fare fallback: {model.FareFallback.Intercept} + {model.FareFallback.PerKm}/km MVR (R2 {model.FareFallback.R2}, n={model.FareFallback.Samples})");
        ctx.Output.WriteLine($"  wrote {path}");
        return 0;
    }

    private static int BuildSeaSpeeds(CliContext ctx)
    {
        var dir = Path.Combine(ctx.DatasetsDir, FollowMeDir);
        Directory.CreateDirectory(ctx.CacheDir);
        var partials = new List<SeaPartial>();
        foreach (var file in Directory.GetFiles(dir, "*.csv").OrderBy(f => f, StringComparer.Ordinal))
        {
            var info = new FileInfo(file);
            var cachePath = Path.Combine(ctx.CacheDir, $"followme-{info.Name}-{info.Length}-{info.LastWriteTimeUtc.Ticks}.json");
            SeaPartial? partial = null;
            if (File.Exists(cachePath))
            {
                try { partial = SeaSpeedBuilder.DeserializePartial(File.ReadAllText(cachePath)); }
                catch (JsonException) { partial = null; }
            }
            if (partial is null)
            {
                ctx.Output.WriteLine($"scanning {info.Name} ...");
                using var csv = CsvStream.Open(file);
                partial = SeaSpeedBuilder.ScanFile(csv);
                var tmp = cachePath + ".tmp";
                File.WriteAllText(tmp, SeaSpeedBuilder.SerializePartial(partial));
                File.Move(tmp, cachePath, overwrite: true);
            }
            else
            {
                ctx.Output.WriteLine($"cached   {info.Name}");
            }
            partials.Add(partial);
        }
        var output = SeaSpeedBuilder.Combine(partials);
        var path = Path.Combine(ctx.SeedDir, "sea-speeds.json");
        WriteJson(path, output);
        var size = new FileInfo(path).Length;
        if (size > 5_000_000) throw new InvalidDataException($"sea-speeds.json is {size} bytes, over the 5 MB seed limit; raise the cell size or device threshold.");
        ctx.Output.WriteLine($"sea-speeds: {output.Rows} pings from {output.FilesProcessed} files -> {output.Cells.Count} cells (>= {output.MinDevices} devices), "
                             + $"moving speed p50/p90 {output.GlobalMovingP50}/{output.GlobalMovingP90}; wrote {path} ({size / 1000} kB)");
        return 0;
    }

    private static int BuildAirSeasonality(CliContext ctx)
    {
        using var csv = CsvStream.Open(Path.Combine(ctx.DatasetsDir, MaclFile));
        // 2022-2025 are complete, post-pandemic years; 2026 is partial, 2020-2021 are distorted.
        var output = AirSeasonalityBuilder.Build(csv, [2022, 2023, 2024, 2025]);
        var path = Path.Combine(ctx.SeedDir, "air-seasonality.json");
        WriteJson(path, output);
        ctx.Output.WriteLine($"air-seasonality: month index {string.Join(" ", output.MonthIndex.Select(m => m.ToString("0.00", CultureInfo.InvariantCulture)))}; wrote {path}");
        return 0;
    }

    private static int ValidateSeed(CliContext ctx)
    {
        var data = SeedFiles.Load(ctx.SeedDir);
        ctx.Output.WriteLine($"seed ok: {data.Places.Count} places, {data.Operators.Count} operators, {data.Services.Count} services, "
                             + $"{data.Links.Count} links, taxi model {(data.Taxi is null ? "absent" : $"{data.Taxi.Fares.Count} fares, {data.Taxi.Durations.Count} duration groups")}, version {data.Version}");
        return 0;
    }

    private static async Task<int> ImportCatalogAsync(CliContext ctx, CancellationToken ct)
    {
        var data = SeedFiles.Load(ctx.SeedDir);
        var configuration = new ConfigurationBuilder().AddEnvironmentVariables().Build();
        var optionsBuilder = new DbContextOptionsBuilder<CatalogDbContext>();
        CatalogModule.Configure(optionsBuilder, configuration.GetDatabaseOptions().ToConnectionString());
        await using var db = new CatalogDbContext(optionsBuilder.Options);
        await db.Database.MigrateAsync(ct);
        var report = await CatalogImporter.ImportAsync(db, data, ct);
        ctx.Output.WriteLine($"import-catalog: version {data.Version}: {report.Added} added, {report.Updated} updated, {report.Removed} removed.");
        return 0;
    }

    private static void WriteJson<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(value, Json) + "\n");
        File.Move(tmp, path, overwrite: true);
    }
}
