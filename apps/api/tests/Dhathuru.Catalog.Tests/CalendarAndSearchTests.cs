using Dhathuru.Catalog.Data;
using Dhathuru.Catalog.Services;
using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Places;
using Dhathuru.Domain.Pricing;
using Dhathuru.Domain.Time;
using Dhathuru.Domain.Transport;

namespace Dhathuru.Catalog.Tests;

public class ServiceCalendarTests
{
    private static CatalogService Make(
        IReadOnlyList<TimeOnly>? times = null, Frequency? frequency = null, int[]? days = null, DateOnly? validFrom = null, DateOnly? validTo = null, int duration = 30) =>
        new(ServiceId.New(), "t", OperatorId.New(), TransportMode.Ferry, PlaceId.New(PlaceType.Jetty), PlaceId.New(PlaceType.Jetty), duration,
            Money.FromMvr(10), PricingKind.Fixed, 10, false, days ?? [1, 2, 3, 4, 5, 6, 7], times ?? [], frequency,
            validFrom ?? new DateOnly(2026, 1, 1), validTo, true, false, null);

    [Fact]
    public void Frequency_expands_to_inclusive_first_and_last()
    {
        var s = Make(frequency: new Frequency(new TimeOnly(5, 30), new TimeOnly(7, 0), 30));
        Assert.Equal(["05:30", "06:00", "06:30", "07:00"], ServiceCalendar.LocalTimes(s).Select(t => t.ToString("HH:mm")));
    }

    [Fact]
    public void Fixed_times_and_frequency_merge_without_duplicates()
    {
        var s = Make(times: [new TimeOnly(6, 0), new TimeOnly(12, 0)], frequency: new Frequency(new TimeOnly(5, 30), new TimeOnly(6, 30), 30));
        Assert.Equal(["05:30", "06:00", "06:30", "12:00"], ServiceCalendar.LocalTimes(s).Select(t => t.ToString("HH:mm")));
    }

    [Fact]
    public void Frequency_ending_at_the_last_minute_of_the_day_does_not_loop()
    {
        var s = Make(frequency: new Frequency(new TimeOnly(23, 0), new TimeOnly(23, 59), 30));
        Assert.Equal(2, ServiceCalendar.LocalTimes(s).Count);
    }

    [Fact]
    public void Run_times_are_maldives_local_converted_to_utc()
    {
        var s = Make(times: [new TimeOnly(8, 30)], duration: 55);
        var run = ServiceCalendar.RunsFor(s, new DateOnly(2026, 10, 27)).Single();
        Assert.Equal(new DateTimeOffset(2026, 10, 27, 3, 30, 0, TimeSpan.Zero), run.DepartAt);
        Assert.Equal(run.DepartAt.AddMinutes(55), run.ArriveAt);
        Assert.Equal($"{s.Id}:2026-10-27T03:30Z", run.ServiceRunId);
    }

    [Fact]
    public void Early_local_departure_belongs_to_the_previous_utc_day_but_the_local_date()
    {
        var s = Make(times: [new TimeOnly(2, 0)]);
        var run = ServiceCalendar.RunsFor(s, new DateOnly(2026, 10, 27)).Single();
        Assert.Equal(new DateOnly(2026, 10, 26), DateOnly.FromDateTime(run.DepartAt.UtcDateTime));
        Assert.Equal(new DateOnly(2026, 10, 27), MaldivesTime.LocalDate(run.DepartAt));
    }

    [Theory]
    [InlineData("2026-10-26", true)]  // Monday
    [InlineData("2026-10-30", false)] // Friday
    [InlineData("2026-11-01", true)]  // Sunday (ISO 7)
    public void Days_of_week_use_iso_numbers(string date, bool runs)
    {
        var s = Make(times: [new TimeOnly(9, 0)], days: [1, 2, 3, 4, 6, 7]);
        Assert.Equal(runs, ServiceCalendar.RunsOn(s, DateOnly.Parse(date)));
    }

    [Fact]
    public void Validity_window_is_inclusive()
    {
        var s = Make(times: [new TimeOnly(9, 0)], validFrom: new DateOnly(2026, 11, 1), validTo: new DateOnly(2026, 11, 3));
        Assert.False(ServiceCalendar.RunsOn(s, new DateOnly(2026, 10, 31)));
        Assert.True(ServiceCalendar.RunsOn(s, new DateOnly(2026, 11, 1)));
        Assert.True(ServiceCalendar.RunsOn(s, new DateOnly(2026, 11, 3)));
        Assert.False(ServiceCalendar.RunsOn(s, new DateOnly(2026, 11, 4)));
    }

    [Fact]
    public void Run_id_round_trips()
    {
        var id = ServiceId.New();
        var at = new DateTimeOffset(2026, 10, 27, 2, 30, 0, TimeSpan.Zero);
        Assert.True(ServiceCalendar.TryParseRunId(ServiceCalendar.RunId(id, at), out var parsedId, out var parsedAt));
        Assert.Equal(id, parsedId);
        Assert.Equal(at, parsedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nonsense")]
    [InlineData("svc_00000000000000000000000000:not-a-time")]
    public void Garbage_run_ids_are_rejected(string? runId) => Assert.False(ServiceCalendar.TryParseRunId(runId, out _, out _));
}

public class PlaceSearchTests
{
    private static readonly IReadOnlyList<CatalogPlace> Places = TestSupport.LoadSeed().Places;

    private static string? Top(string query, PlaceType? type = null) =>
        PlaceSearch.Search(Places, query, type, null).FirstOrDefault()?.Place.Name["en"];

    [Theory]
    [InlineData("maafushi", "Maafushi")]
    [InlineData("Maafushi", "Maafushi")]
    [InlineData("mafushi", "Maafushi")]
    [InlineData("maaf", "Maafushi")]
    [InlineData("maafusi", "Maafushi")]
    [InlineData("tulusdoo", "Thulusdhoo")]
    [InlineData("thulusdu", "Thulusdhoo")]
    [InlineData("hulumale", "Hulhumalé")]
    [InlineData("hulhumale", "Hulhumalé")]
    [InlineData("gurahidhoo", "Guraidhoo")]
    [InlineData("Male", "Malé")]
    public void Partial_and_misspelled_island_names_find_the_island(string query, string expected) =>
        Assert.Equal(expected, Top(query, PlaceType.Island));

    [Fact]
    public void Iata_codes_are_exact_matches()
    {
        Assert.Equal("Velana International Airport", Top("MLE"));
        Assert.Equal("Gan International Airport", Top("gan", PlaceType.Airport));
    }

    [Fact]
    public void Dhivehi_text_matches_its_own_name()
    {
        Assert.Equal("Malé", Top("މާލެ"));
    }

    [Fact]
    public void Nonsense_matches_nothing()
    {
        Assert.Empty(PlaceSearch.Search(Places, "zzzzqqqq", null, null));
    }

    [Fact]
    public void Type_filter_and_blank_query_list_everything_of_that_type()
    {
        var airports = PlaceSearch.Search(Places, "", PlaceType.Airport, null);
        Assert.NotEmpty(airports);
        Assert.All(airports, m => Assert.Equal(PlaceType.Airport, m.Place.Type));
    }

    [Fact]
    public void Near_orders_by_distance_when_scores_tie()
    {
        var near = new GeoPoint(3.94, 73.49); // Maafushi
        var results = PlaceSearch.Search(Places, "", PlaceType.Island, near);
        var ordered = results.Select(r => r.DistanceKm!.Value).ToList();
        Assert.Equal(ordered.Order().ToList(), ordered);
        Assert.Equal("Maafushi", results[0].Place.Name["en"]);
    }

    [Theory]
    [InlineData("kitten", "sitting", 3)]
    [InlineData("abc", "acb", 1)]
    [InlineData("", "abc", 3)]
    public void Edit_distance_counts_swaps_as_one(string a, string b, int expected) =>
        Assert.Equal(expected, PlaceSearch.EditDistance(a, b));
}
