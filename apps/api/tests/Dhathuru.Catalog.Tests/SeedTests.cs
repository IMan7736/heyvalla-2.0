using Dhathuru.Catalog.Data;
using Dhathuru.Domain.Places;
using Dhathuru.Domain.Transport;

namespace Dhathuru.Catalog.Tests;

public class SeedTests
{
    private static readonly CatalogData Data = TestSupport.LoadSeed();

    [Fact]
    public void Real_seed_loads_and_has_every_collection()
    {
        Assert.NotEmpty(Data.Places);
        Assert.NotEmpty(Data.Operators);
        Assert.NotEmpty(Data.Services);
        Assert.NotEmpty(Data.Links);
        Assert.NotNull(Data.Taxi);
        Assert.Equal(6, Data.ReliabilityPriors.Count);
    }

    [Fact]
    public void Every_scheduled_service_is_flagged_synthetic_and_unverified()
    {
        // No licensed source of real timetables exists (docs/data/SOURCES.md): nothing scheduled may claim to be real.
        foreach (var s in Data.Services.Where(s => !s.OnDemand))
        {
            Assert.True(s.Synthetic, $"{s.Slug} must be synthetic");
            Assert.False(s.CutoffVerified, $"{s.Slug} cutoff must be unverified");
        }
    }

    [Fact]
    public void Reliability_priors_are_synthetic_with_no_samples()
    {
        Assert.All(Data.ReliabilityPriors, p =>
        {
            Assert.True(p.Synthetic);
            Assert.Equal(0, p.Samples);
        });
    }

    [Fact]
    public void Taxi_zones_exist_and_domestic_flights_leave_from_the_airport()
    {
        var zoneKeys = Data.Places.Where(p => p.ZoneKey is not null).Select(p => p.ZoneKey).ToHashSet();
        Assert.Contains("male", zoneKeys);
        Assert.Contains("velana", zoneKeys);
        var velana = Data.Places.Single(p => p.Code == "MLE");
        Assert.Equal(PlaceType.Airport, velana.Type);
        Assert.Contains(Data.Services, s => s.Mode == TransportMode.DomesticFlight && s.From == velana.Id);
    }

    [Fact]
    public void Taxi_durations_never_publish_groups_smaller_than_ten()
    {
        Assert.All(Data.Taxi!.Durations, d => Assert.True(d.Samples >= TaxiModel.MinSamples));
    }

    [Fact]
    public void Taxi_fare_matrix_is_symmetric_and_in_whole_rufiyaa()
    {
        foreach (var f in Data.Taxi!.Fares)
        {
            Assert.Equal(0, f.FareMinor % 100);
            Assert.Equal(f.FareMinor, Data.Taxi.Fares.Single(x => x.FromZone == f.ToZone && x.ToZone == f.FromZone).FareMinor);
        }
        Assert.Equal(36, Data.Taxi.Fares.Count);
    }

    [Fact]
    public void Dhivehi_names_are_carried_as_localised_text()
    {
        var male = Data.Places.Single(p => p.Slug == "male");
        Assert.Equal("Malé", male.Name["en"]);
        Assert.True(male.Name.ContainsKey("dv"));
    }

    [Fact]
    public void Seed_files_stay_under_the_five_megabyte_limit()
    {
        foreach (var file in Directory.GetFiles(TestSupport.SeedDirectory(), "*.json"))
            Assert.True(new FileInfo(file).Length < 5_000_000, file);
    }

    [Fact]
    public void Seed_files_contain_no_identifiers_or_raw_coordinates_of_rides_or_vessels()
    {
        foreach (var name in new[] { "taxi-model.json", "sea-speeds.json", "air-seasonality.json" })
        {
            var path = Path.Combine(TestSupport.SeedDirectory(), name);
            if (!File.Exists(path)) continue;
            var text = File.ReadAllText(path);
            foreach (var forbidden in new[] { "imei", "pickup_lat", "dropoff_lat", "poll_id", "carrier_", "Route_" })
                Assert.DoesNotContain(forbidden, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Malformed_seed_is_rejected_with_a_clear_message()
    {
        var json = File.ReadAllText(Path.Combine(TestSupport.SeedDirectory(), SeedFiles.CatalogFile));
        var broken = json.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 9", StringComparison.Ordinal);
        var ex = Assert.Throws<SeedValidationException>(() => SeedFiles.Parse(broken));
        Assert.Contains("schemaVersion", ex.Message);
    }

    [Fact]
    public void Dangling_references_are_rejected()
    {
        var node = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(Path.Combine(TestSupport.SeedDirectory(), SeedFiles.CatalogFile)))!;
        var service = node["services"]!.AsArray().First(s => s!["fromId"] is not null)!;
        service["fromId"] = "isl_00000000000000000000000000";
        var ex = Assert.Throws<SeedValidationException>(() => SeedFiles.Parse(node.ToJsonString()));
        Assert.Contains("not found", ex.Message);
    }

    [Fact]
    public void Same_input_gives_same_version_so_imports_are_reproducible()
    {
        var again = TestSupport.LoadSeed();
        Assert.Equal(Data.Version, again.Version);
    }
}
