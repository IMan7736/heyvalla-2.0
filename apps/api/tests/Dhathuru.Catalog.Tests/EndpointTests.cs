using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dhathuru.Catalog.Data;
using Dhathuru.Catalog.Endpoints;
using Dhathuru.Catalog.Services;
using Dhathuru.Domain.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Dhathuru.Catalog.Tests;

public sealed class CatalogApiFixture : IAsyncLifetime
{
    private WebApplication? app;

    public HttpClient Client { get; private set; } = null!;

    public CatalogData Data { get; } = TestSupport.LoadSeed();

    public async ValueTask InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddSingleton<ICatalogDataSource>(new StaticCatalogDataSource(Data));
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<CatalogCache>();
        builder.Services.AddProblemDetails();
        var built = builder.Build();
        // Same shape as the real host: ProblemException becomes problem+json with a code.
        built.Use(async (context, next) =>
        {
            try
            {
                await next(context);
            }
            catch (ProblemException ex)
            {
                context.Response.StatusCode = ex.Status;
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsJsonAsync(new { status = ex.Status, code = ex.Code, errors = ex.Errors.Select(e => new { e.Field, e.Code }) });
            }
        });
        built.MapGroup("/v1").MapCatalogEndpoints();
        await built.StartAsync();
        app = built;
        Client = built.GetTestClient();
    }

    public async ValueTask DisposeAsync()
    {
        if (app is not null) await app.DisposeAsync();
    }
}

public class EndpointTests(CatalogApiFixture api) : IClassFixture<CatalogApiFixture>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<JsonElement> GetJson(string url, HttpStatusCode expected = HttpStatusCode.OK)
    {
        var response = await api.Client.GetAsync(url, Ct);
        Assert.Equal(expected, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
    }

    [Fact]
    public async Task Search_finds_a_misspelled_island_and_shapes_the_response_like_the_contract()
    {
        var json = await GetJson("/v1/places?query=mafushi&type=island");
        var first = json.GetProperty("items")[0];
        Assert.Equal("Maafushi", first.GetProperty("name").GetProperty("en").GetString());
        Assert.Equal("island", first.GetProperty("type").GetString());
        Assert.True(first.GetProperty("geo").GetProperty("lat").GetDouble() is > 3 and < 5);
        Assert.StartsWith("isl_", first.GetProperty("id").GetString());
        Assert.True(first.TryGetProperty("isHub", out _));
        Assert.Equal(50, json.GetProperty("page").GetProperty("limit").GetInt32());
        Assert.False(first.TryGetProperty("code", out _), "null fields are omitted");
    }

    [Fact]
    public async Task Pagination_walks_through_all_results_without_repeats()
    {
        var seen = new List<string>();
        string? cursor = null;
        do
        {
            var json = await GetJson($"/v1/places?limit=7{(cursor is null ? "" : "&cursor=" + cursor)}");
            seen.AddRange(json.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetString()!));
            var next = json.GetProperty("page").GetProperty("nextCursor");
            cursor = next.ValueKind == JsonValueKind.Null ? null : next.GetString();
        }
        while (cursor is not null);
        Assert.Equal(api.Data.Places.Count, seen.Count);
        Assert.Equal(seen.Count, seen.Distinct().Count());
    }

    [Fact]
    public async Task Near_sorts_by_distance()
    {
        var json = await GetJson("/v1/places?type=island&near=3.94,73.49&limit=1");
        Assert.Equal("Maafushi", json.GetProperty("items")[0].GetProperty("name").GetProperty("en").GetString());
    }

    [Theory]
    [InlineData("/v1/places?type=spaceport", "type", "INVALID_PLACE_TYPE")]
    [InlineData("/v1/places?near=abc", "near", "INVALID_COORDINATES")]
    [InlineData("/v1/places?near=99,200", "near", "INVALID_COORDINATES")]
    [InlineData("/v1/places?limit=0", "limit", "OUT_OF_RANGE")]
    [InlineData("/v1/places?limit=500", "limit", "OUT_OF_RANGE")]
    [InlineData("/v1/places?cursor=!!!", "cursor", "INVALID_CURSOR")]
    public async Task Bad_search_input_is_a_422_with_field_codes(string url, string field, string code)
    {
        var json = await GetJson(url, HttpStatusCode.UnprocessableEntity);
        Assert.Equal("VALIDATION_FAILED", json.GetProperty("code").GetString());
        Assert.Contains(json.GetProperty("errors").EnumerateArray(), e => e.GetProperty("field").GetString() == field && e.GetProperty("code").GetString() == code);
    }

    [Fact]
    public async Task Get_place_returns_it_and_unknown_or_malformed_ids_are_404()
    {
        var velana = api.Data.Places.Single(p => p.Code == "MLE");
        var json = await GetJson($"/v1/places/{velana.Id}");
        Assert.Equal("MLE", json.GetProperty("code").GetString());
        Assert.True(json.GetProperty("isHub").GetBoolean());
        await GetJson("/v1/places/isl_00000000000000000000000000", HttpStatusCode.NotFound);
        await GetJson("/v1/places/nonsense", HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Operators_list_modes_and_provider_kind()
    {
        var json = await GetJson("/v1/operators");
        var items = json.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(api.Data.Operators.Count, items.Count);
        Assert.All(items, o => Assert.Contains(o.GetProperty("providerKind").GetString(), new[] { "static", "mock", "live" }));
        Assert.Contains(items, o => o.GetProperty("modes").EnumerateArray().Any(m => m.GetString() == "domestic_flight"));
    }

    [Fact]
    public async Task Services_between_two_places_list_departures_for_the_local_date_and_say_they_are_synthetic()
    {
        var svc = api.Data.Services.Single(s => s.Slug == "speedboat-male-maafushi-out");
        var json = await GetJson($"/v1/services?from={svc.From}&to={svc.To}&date=2026-10-27");
        var item = json.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetString() == svc.Id.Value);
        var departures = item.GetProperty("departures").EnumerateArray().ToList();
        Assert.Equal(4, departures.Count);
        Assert.Equal(new DateTimeOffset(2026, 10, 27, 3, 30, 0, TimeSpan.Zero), departures[0].GetProperty("departAt").GetDateTimeOffset());
        Assert.StartsWith($"{svc.Id}:2026-10-27T03:30Z", departures[0].GetProperty("serviceRunId").GetString());
        Assert.True(item.GetProperty("synthetic").GetBoolean());
        Assert.Equal("scheduled", item.GetProperty("kind").GetString());
        Assert.Equal(35000, item.GetProperty("fare").GetProperty("base").GetProperty("amountMinor").GetInt64());
        Assert.Equal("MVR", item.GetProperty("fare").GetProperty("base").GetProperty("currency").GetString());
        Assert.Equal(20, item.GetProperty("checkInCutoffMin").GetInt32());
    }

    [Fact]
    public async Task Services_on_a_day_the_service_does_not_run_have_no_departures()
    {
        var svc = api.Data.Services.Single(s => s.Slug == "mtcc-ferry-male-rasdhoo-out");
        var json = await GetJson($"/v1/services?from={svc.From}&to={svc.To}&date=2026-10-30"); // Friday
        var item = json.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetString() == svc.Id.Value);
        Assert.Empty(item.GetProperty("departures").EnumerateArray());
    }

    [Fact]
    public async Task Services_between_unconnected_places_is_an_empty_list_not_an_error()
    {
        var a = api.Data.Places.Single(p => p.Slug == "maafushi").Id;
        var b = api.Data.Places.Single(p => p.Slug == "apt-gan").Id;
        var json = await GetJson($"/v1/services?from={a}&to={b}&date=2026-10-27");
        Assert.Empty(json.GetProperty("items").EnumerateArray());
    }

    [Theory]
    [InlineData("/v1/services?from=x&to=y&date=2026-10-27")]
    [InlineData("/v1/services?date=2026-10-27")]
    [InlineData("/v1/services?from=isl_00000000000000000000000000&to=isl_00000000000000000000000000&date=27-10-2026")]
    public async Task Services_validates_its_input(string url)
    {
        var json = await GetJson(url, HttpStatusCode.UnprocessableEntity);
        Assert.Equal("VALIDATION_FAILED", json.GetProperty("code").GetString());
    }

    [Fact]
    public void Cursor_codec_round_trips_and_rejects_garbage()
    {
        Assert.True(CatalogEndpoints.TryDecodeCursor(CatalogEndpoints.EncodeCursor(1234), out var offset));
        Assert.Equal(1234, offset);
        Assert.False(CatalogEndpoints.TryDecodeCursor("o:5", out _));
        Assert.False(CatalogEndpoints.TryDecodeCursor("", out _));
    }
}
