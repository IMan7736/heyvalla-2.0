using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Dhathuru.Api.Tests.Support;
using Dhathuru.Bookings.Services;
using Dhathuru.Domain.Time;
using Microsoft.Extensions.DependencyInjection;

namespace Dhathuru.Api.Tests;

/// <summary>Search, re-read an option, draw its legs and book it, through the real host with the seeded catalog.</summary>
public sealed class TripsAndBookingFlowTests(PostgresFixture database) : IDisposable
{
    private readonly ApiFactory api = new(database, new Dictionary<string, string?>
    {
        ["Catalog:Source"] = "seed",
        ["Catalog:SeedDirectory"] = SeedDirectory(),
    });

    public void Dispose() => api.Dispose();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string SeedDirectory()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "seed"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new DirectoryNotFoundException("apps/api/seed not found"), "seed");
    }

    private async Task<string> PlaceAsync(string query, string type)
    {
        var page = await api.Anonymous().GetFromJsonAsync<JsonElement>($"/v1/places?query={Uri.EscapeDataString(query)}&type={type}&limit=1", Ct);
        return page.GetProperty("items")[0].GetProperty("id").GetString()!;
    }

    private async Task<HttpResponseMessage> SearchAsync(string from, string to, int adults = 2)
    {
        var date = MaldivesTime.LocalDate(DateTimeOffset.UtcNow).AddDays(1).ToString("yyyy-MM-dd");
        return await api.Anonymous().PostAsJsonAsync("/v1/trips/search", new
        {
            from, to, date, time = "06:00", timeMode = "departAfter",
            passengers = new { adults, children = 0, infants = 0 },
            preferences = new { riskTolerance = "balanced" },
        }, Ct);
    }

    [Fact]
    public async Task Providers_are_registered_for_bookings_to_find_by_id()
    {
        var registry = api.Services.GetRequiredService<ProviderRegistry>();
        foreach (var id in new[] { "mtcc", "manta-air", "maldivian", "avas", "speedboats" })
        {
            Assert.NotNull(registry.Find(id));
        }
    }

    [Fact]
    public async Task Search_returns_ranked_options_that_can_be_read_back_and_drawn()
    {
        var response = await SearchAsync(await PlaceAsync("Maafushi", "island"), await PlaceAsync("Kooddoo", "island"));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.JsonAsync();
        var options = body.GetProperty("options");
        Assert.True(options.GetArrayLength() > 0);
        Assert.StartsWith("srch_", body.GetProperty("searchId").GetString());
        Assert.Equal(JsonValueKind.Array, body.GetProperty("notices").ValueKind);

        var option = options[0];
        var again = await api.Anonymous().GetAsync($"/v1/trips/options/{option.GetProperty("id").GetString()}", Ct);
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(option.GetProperty("id").GetString(), (await again.JsonAsync()).GetProperty("id").GetString());

        var legIds = string.Join(',', option.GetProperty("legs").EnumerateArray().Take(2).Select(l => l.GetProperty("id").GetString()));
        var geo = await api.Anonymous().GetAsync($"/v1/geo/legs?ids={legIds}", Ct);
        Assert.Equal(HttpStatusCode.OK, geo.StatusCode);
        Assert.Equal("application/geo+json", geo.Content.Headers.ContentType?.MediaType);
        var features = (await geo.JsonAsync()).GetProperty("features");
        Assert.Equal(2, features.GetArrayLength());
        Assert.True(features[0].GetProperty("properties").GetProperty("approximate").GetBoolean());
    }

    [Fact]
    public async Task Search_rejects_bad_input_with_field_codes()
    {
        var response = await api.Anonymous().PostAsJsonAsync("/v1/trips/search", new { from = "nope", passengers = new { adults = 0 } }, Ct);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var fields = (await response.JsonAsync()).GetProperty("errors").EnumerateArray().Select(e => e.GetProperty("field").GetString()).ToList();
        Assert.Contains("from", fields);
        Assert.Contains("passengers.adults", fields);
    }

    [Fact]
    public async Task Unknown_option_leg_and_anonymous_replan_are_refused()
    {
        var missing = await api.Anonymous().GetAsync("/v1/trips/options/trp_00000000000000000000000000", Ct);
        Assert.Equal("OPTION_NOT_FOUND", await missing.CodeAsync());
        Assert.Equal(HttpStatusCode.NotFound, (await api.Anonymous().GetAsync("/v1/geo/legs?ids=leg_00000000000000000000000000", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await api.Anonymous().GetAsync("/v1/geo/legs", Ct)).StatusCode);
        var replan = await api.Anonymous().PostAsJsonAsync("/v1/trips/replan", new { }, Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, replan.StatusCode);
    }

    [Fact]
    public async Task A_searched_option_can_be_booked()
    {
        var user = await api.RegisterAndLoginAsync(verify: true);
        var search = await (await SearchAsync(await PlaceAsync("Maafushi", "island"), await PlaceAsync("Kooddoo", "island"))).JsonAsync();
        var option = search.GetProperty("options")[0].GetProperty("id").GetString();

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/bookings")
        {
            Content = JsonContent.Create(new
            {
                optionId = option,
                passengers = new[]
                {
                    new { fullName = "Ali Hassan", passengerType = "adult" },
                    new { fullName = "Aisha Ali", passengerType = "adult" },
                },
            }),
        };
        request.Headers.Add("Idempotency-Key", $"create-{Guid.NewGuid():N}");
        var response = await api.As(user.Tokens.AccessToken).SendAsync(request, Ct);

        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync(Ct));
        var booking = await response.JsonAsync();
        Assert.StartsWith("bkg_", booking.GetProperty("id").GetString());
    }

    [Fact]
    public async Task Paying_confirms_the_booking_and_emails_the_itinerary()
    {
        var user = await api.RegisterAndLoginAsync(verify: true);
        var search = await (await SearchAsync(await PlaceAsync("Maafushi", "island"), await PlaceAsync("Kooddoo", "island"))).JsonAsync();
        var client = api.As(user.Tokens.AccessToken);

        var create = new HttpRequestMessage(HttpMethod.Post, "/v1/bookings")
        {
            Content = JsonContent.Create(new
            {
                optionId = search.GetProperty("options")[0].GetProperty("id").GetString(),
                passengers = new[] { new { fullName = "Ali Hassan", passengerType = "adult" }, new { fullName = "Aisha Ali", passengerType = "adult" } },
            }),
        };
        create.Headers.Add("Idempotency-Key", $"create-{Guid.NewGuid():N}");
        var booking = await (await client.SendAsync(create, Ct)).JsonAsync();

        var pay = new HttpRequestMessage(HttpMethod.Post, $"/v1/bookings/{booking.GetProperty("id").GetString()}/pay")
        {
            Content = JsonContent.Create(new { method = "card", paymentToken = "tok_fake_success" }),
        };
        pay.Headers.Add("Idempotency-Key", $"pay-{Guid.NewGuid():N}");
        var paid = await client.SendAsync(pay, Ct);
        Assert.True(paid.IsSuccessStatusCode, await paid.Content.ReadAsStringAsync(Ct));

        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (api.Emails.Count(user.Email, "booking.confirmed") == 0 && DateTime.UtcNow < deadline) await Task.Delay(100, Ct);
        var email = api.Emails.Last(user.Email, "booking.confirmed");
        Assert.NotEmpty(email.Attachments ?? []);
        var rendered = Dhathuru.Api.Email.EmailTemplates.Render(email);
        Assert.Equal("Your trip is booked", rendered.Subject);
        Assert.Contains("Booking", rendered.Text);
    }
}
