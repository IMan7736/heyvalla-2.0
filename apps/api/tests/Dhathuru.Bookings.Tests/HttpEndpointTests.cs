using System.Net;
using System.Net.Http.Json;
using Dhathuru.Bookings.Payments;
using Dhathuru.Bookings.Tickets;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dhathuru.Bookings.Tests;

/// <summary>The HTTP surface: authentication, verified email, ownership (IDOR), idempotency headers and wire shapes.</summary>
[Collection("postgres")]
public class HttpEndpointTests(PostgresFixture pg)
{
    private static object Body(Dhathuru.Domain.Trips.TripOption option, int adults = 1) => new
    {
        optionId = option.Id.Value,
        passengers = Enumerable.Range(1, adults).Select(i => new { fullName = $"Adult {i}", passengerType = "adult" }).ToArray(),
    };

    private static object Token(string token = FakeTokens.Success) => new { method = "card", paymentToken = token };

    private static async Task<string> BookAndPay(HttpClient client, Dhathuru.Domain.Trips.TripOption option)
    {
        var created = await client.PostWithKey("/v1/bookings", Body(option));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var id = (await created.Body()).GetProperty("id").GetString()!;
        var paid = await client.PostWithKey($"/v1/bookings/{id}/pay", Token());
        Assert.Equal(HttpStatusCode.OK, paid.StatusCode);
        Assert.Equal("confirmed", (await paid.Body()).GetProperty("status").GetString());
        return id;
    }

    [Fact]
    public async Task Every_booking_route_needs_a_signed_in_user()
    {
        await using var h = await Harness.CreateAsync(pg);
        await using var host = await HttpHost.StartAsync(h);
        using var anonymous = host.Client();
        foreach (var path in new[] { "/v1/bookings", "/v1/bookings/bkg_X", "/v1/bookings/bkg_X/tickets", "/v1/bookings/bkg_X/cancellation-quote", "/v1/bookings/bkg_X/itinerary.pdf", "/v1/tickets/tkt_X/qr" })
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path, TestContext.Current.CancellationToken)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostWithKey("/v1/bookings", new { })).StatusCode);
    }

    [Fact]
    public async Task Booking_and_paying_need_a_verified_email_but_reading_does_not()
    {
        await using var h = await Harness.CreateAsync(pg);
        await using var host = await HttpHost.StartAsync(h);
        var option = h.AddTwoLegOption();
        using var unverified = host.Client("usr_unverified", verified: false);
        Assert.Equal(HttpStatusCode.Forbidden, (await unverified.PostWithKey("/v1/bookings", Body(option))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await unverified.PostWithKey("/v1/bookings/bkg_X/pay", Token())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await unverified.GetAsync("/v1/bookings", TestContext.Current.CancellationToken)).StatusCode);
    }

    [Fact]
    public async Task Create_and_pay_require_an_idempotency_key()
    {
        await using var h = await Harness.CreateAsync(pg);
        await using var host = await HttpHost.StartAsync(h);
        var option = h.AddTwoLegOption();
        using var client = host.Client("usr_a");
        var response = await client.PostAsJsonAsync("/v1/bookings", Body(option), HttpExtensions.Json, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("VALIDATION_FAILED", (await response.Body()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task The_full_happy_path_over_http_returns_the_documented_shapes()
    {
        await using var h = await Harness.CreateAsync(pg);
        await using var host = await HttpHost.StartAsync(h);
        var option = h.AddTwoLegOption();
        using var client = host.Client("usr_a");

        var key = Guid.NewGuid().ToString();
        var first = await client.PostWithKey("/v1/bookings", Body(option), key);
        var replay = await client.PostWithKey("/v1/bookings", Body(option), key);
        var held = await first.Body();
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(held.GetProperty("id").GetString(), (await replay.Body()).GetProperty("id").GetString());
        Assert.Equal("held", held.GetProperty("status").GetString());
        Assert.Equal(94000, held.GetProperty("total").GetProperty("amountMinor").GetInt64());
        Assert.Equal(2, held.GetProperty("legs").GetArrayLength());
        Assert.Equal(2, held.GetProperty("priceBreakdown").GetArrayLength());
        Assert.Equal(0, held.GetProperty("disruptions").GetArrayLength());
        var id = held.GetProperty("id").GetString()!;

        var quote = await client.GetAsync($"/v1/bookings/{id}/cancellation-quote", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, quote.StatusCode);

        var paid = await client.PostWithKey($"/v1/bookings/{id}/pay", Token());
        var body = await paid.Body();
        Assert.Equal("confirmed", body.GetProperty("status").GetString());
        Assert.Equal("succeeded", body.GetProperty("payment").GetProperty("status").GetString());

        var tickets = await (await client.GetAsync($"/v1/bookings/{id}/tickets", TestContext.Current.CancellationToken)).Body();
        var items = tickets.GetProperty("items");
        Assert.Equal(2, items.GetArrayLength());
        var ticketId = items[0].GetProperty("id").GetString()!;

        var qr = await client.GetAsync($"/v1/tickets/{ticketId}/qr", TestContext.Current.CancellationToken);
        Assert.Equal("image/svg+xml", qr.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("<svg", await qr.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        var pdf = await client.GetAsync($"/v1/bookings/{id}/itinerary.pdf", TestContext.Current.CancellationToken);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString((await pdf.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken))[..5]));

        var list = await (await client.GetAsync("/v1/bookings", TestContext.Current.CancellationToken)).Body();
        Assert.Equal(1, list.GetProperty("items").GetArrayLength());

        var cancelled = await client.PostWithKey($"/v1/bookings/{id}/cancel", new { });
        var cancelledBody = await cancelled.Body();
        Assert.Equal("cancelled", cancelledBody.GetProperty("status").GetString());
        Assert.True(cancelledBody.GetProperty("payment").GetProperty("refunded").GetProperty("amountMinor").GetInt64() > 0);
    }

    [Fact]
    public async Task Another_user_gets_404_for_every_route_on_someone_elses_booking()
    {
        await using var h = await Harness.CreateAsync(pg);
        await using var host = await HttpHost.StartAsync(h);
        var option = h.AddTwoLegOption();
        using var owner = host.Client("usr_owner");
        using var intruder = host.Client("usr_intruder");
        var id = await BookAndPay(owner, option);
        var tickets = await (await owner.GetAsync($"/v1/bookings/{id}/tickets", TestContext.Current.CancellationToken)).Body();
        var ticketId = tickets.GetProperty("items")[0].GetProperty("id").GetString()!;

        foreach (var path in new[] { $"/v1/bookings/{id}", $"/v1/bookings/{id}/tickets", $"/v1/bookings/{id}/cancellation-quote", $"/v1/bookings/{id}/itinerary.pdf", $"/v1/tickets/{ticketId}/qr" })
        {
            var response = await intruder.GetAsync(path, TestContext.Current.CancellationToken);
            Assert.True(response.StatusCode == HttpStatusCode.NotFound, $"{path} answered {(int)response.StatusCode}");
            Assert.Equal("NOT_FOUND", (await response.Body()).GetProperty("code").GetString());
        }
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.PostWithKey($"/v1/bookings/{id}/pay", Token())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.PostWithKey($"/v1/bookings/{id}/pay/verify", new { code = "123456" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.PostWithKey($"/v1/bookings/{id}/cancel", new { })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await intruder.PostWithKey($"/v1/bookings/{id}/disruptions/dsr_X/acknowledge", new { })).StatusCode);
        Assert.Equal(0, (await (await intruder.GetAsync("/v1/bookings", TestContext.Current.CancellationToken)).Body()).GetProperty("items").GetArrayLength());

        // The owner's booking is untouched.
        Assert.Equal("confirmed", (await (await owner.GetAsync($"/v1/bookings/{id}", TestContext.Current.CancellationToken)).Body()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task A_decline_over_http_leaves_the_booking_held_and_reports_why()
    {
        await using var h = await Harness.CreateAsync(pg);
        await using var host = await HttpHost.StartAsync(h);
        var option = h.AddTwoLegOption();
        using var client = host.Client("usr_a");
        var created = await (await client.PostWithKey("/v1/bookings", Body(option))).Body();
        var id = created.GetProperty("id").GetString()!;
        var declined = await (await client.PostWithKey($"/v1/bookings/{id}/pay", Token(FakeTokens.Declined))).Body();
        Assert.Equal("held", declined.GetProperty("status").GetString());
        Assert.Equal("failed", declined.GetProperty("payment").GetProperty("status").GetString());
        Assert.Equal("CARD_DECLINED", declined.GetProperty("payment").GetProperty("failureCode").GetString());
        var retried = await (await client.PostWithKey($"/v1/bookings/{id}/pay", Token())).Body();
        Assert.Equal("confirmed", retried.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Verification_flow_over_http()
    {
        await using var h = await Harness.CreateAsync(pg);
        await using var host = await HttpHost.StartAsync(h);
        var option = h.AddTwoLegOption();
        using var client = host.Client("usr_a");
        var id = (await (await client.PostWithKey("/v1/bookings", Body(option))).Body()).GetProperty("id").GetString()!;
        var challenge = await (await client.PostWithKey($"/v1/bookings/{id}/pay", Token(FakeTokens.Verification))).Body();
        Assert.Equal("requires_action", challenge.GetProperty("payment").GetProperty("status").GetString());
        Assert.Equal("verification_code", challenge.GetProperty("payment").GetProperty("challenge").GetProperty("kind").GetString());
        var done = await client.PostWithKey($"/v1/bookings/{id}/pay/verify", new { code = "123456" });
        Assert.Equal("confirmed", (await done.Body()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Ticket_verification_is_open_but_consuming_needs_the_operator_key()
    {
        await using var h = await Harness.CreateAsync(pg);
        await using var host = await HttpHost.StartAsync(h);
        var option = h.AddTwoLegOption();
        using var owner = host.Client("usr_a");
        var id = await BookAndPay(owner, option);

        string payload;
        await using (var scope = h.Scope())
        {
            var ticket = await scope.ServiceProvider.GetRequiredService<BookingsDbContext>().Tickets.AsNoTracking().FirstAsync(t => t.BookingId == id, TestContext.Current.CancellationToken);
            payload = scope.ServiceProvider.GetRequiredService<TicketSigner>().Sign(ticket.Id, ticket.Nonce, ticket.KeyId);
        }

        using var scanner = host.Client();
        var check = await (await scanner.PostWithKey("/v1/tickets/verify", new { payload, consume = false })).Body();
        Assert.True(check.GetProperty("valid").GetBoolean());

        var noKey = await scanner.PostWithKey("/v1/tickets/verify", new { payload, consume = true });
        Assert.False(noKey.IsSuccessStatusCode);

        var request = new HttpRequestMessage(HttpMethod.Post, "/v1/tickets/verify") { Content = JsonContent.Create(new { payload, consume = true }, options: HttpExtensions.Json) };
        request.Headers.Add("X-Operator-Key", "operator-test-key");
        var consumed = await (await scanner.SendAsync(request, TestContext.Current.CancellationToken)).Body();
        Assert.True(consumed.GetProperty("valid").GetBoolean());

        var again = await (await scanner.PostWithKey("/v1/tickets/verify", new { payload, consume = false })).Body();
        Assert.False(again.GetProperty("valid").GetBoolean());
        Assert.Equal("TICKET_ALREADY_USED", again.GetProperty("reasonCode").GetString());

        var forged = await (await scanner.PostWithKey("/v1/tickets/verify", new { payload = payload[..^3] + "AAA", consume = false })).Body();
        Assert.False(forged.GetProperty("valid").GetBoolean());
    }

    [Fact]
    public async Task The_simulation_endpoint_is_invisible_unless_switched_on()
    {
        await using var off = await Harness.CreateAsync(pg, simulation: false);
        await using var offHost = await HttpHost.StartAsync(off);
        using var a = offHost.Client("usr_a");
        Assert.Equal(HttpStatusCode.NotFound, (await a.PostAsJsonAsync("/v1/dev/provider-events", new { providerId = "fake-ferry", serviceRunId = "ferry-run-1", state = "delayed", delayMinutes = 30 }, HttpExtensions.Json, TestContext.Current.CancellationToken)).StatusCode);

        await using var on = await Harness.CreateAsync(pg, simulation: true);
        await using var onHost = await HttpHost.StartAsync(on);
        using var b = onHost.Client("usr_b");
        var response = await b.PostAsJsonAsync("/v1/dev/provider-events", new { providerId = "fake-ferry", serviceRunId = "ferry-run-1", state = "delayed", delayMinutes = 30 }, HttpExtensions.Json, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_disruption_shows_up_on_the_booking_and_can_be_acknowledged_by_its_owner_only()
    {
        await using var h = await Harness.CreateAsync(pg, simulation: true);
        await using var host = await HttpHost.StartAsync(h);
        var option = h.AddTwoLegOption();
        using var owner = host.Client("usr_a");
        using var other = host.Client("usr_b");
        var id = await BookAndPay(owner, option);

        var event_ = await owner.PostAsJsonAsync("/v1/dev/provider-events", new { providerId = "fake-ferry", serviceRunId = "ferry-run-1", state = "delayed", delayMinutes = 45 }, HttpExtensions.Json, TestContext.Current.CancellationToken);
        Assert.Equal(1, (await event_.Body()).GetProperty("affected").GetInt32());

        var booking = await (await owner.GetAsync($"/v1/bookings/{id}", TestContext.Current.CancellationToken)).Body();
        var disruption = booking.GetProperty("disruptions")[0];
        Assert.Equal("delayed", disruption.GetProperty("kind").GetString());
        Assert.Equal(45, disruption.GetProperty("delayMinutes").GetInt32());
        Assert.Equal("open", disruption.GetProperty("state").GetString());
        var disruptionId = disruption.GetProperty("id").GetString()!;

        Assert.Equal(HttpStatusCode.NotFound, (await other.PostWithKey($"/v1/bookings/{id}/disruptions/{disruptionId}/acknowledge", new { })).StatusCode);
        var acked = await (await owner.PostWithKey($"/v1/bookings/{id}/disruptions/{disruptionId}/acknowledge", new { })).Body();
        Assert.Equal("acknowledged", acked.GetProperty("disruptions")[0].GetProperty("state").GetString());
        Assert.Equal("confirmed", acked.GetProperty("status").GetString()); // never rebooked or cancelled for the traveller
    }
}
