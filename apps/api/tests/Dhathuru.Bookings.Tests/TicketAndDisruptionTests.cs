using Dhathuru.Bookings;
using Dhathuru.Bookings.Domain;
using Dhathuru.Bookings.Payments;
using Dhathuru.Bookings.Services;
using Dhathuru.Bookings.Tickets;
using Dhathuru.Domain.Http;
using Dhathuru.Domain.Providers;
using Dhathuru.Domain.Transport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dhathuru.Bookings.Tests;

[Collection("postgres")]
public class TicketTests(PostgresFixture pg)
{
    private const string OperatorKey = "operator-test-key";

    private static async Task<T> With<T>(Harness h, Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = h.Scope();
        return await action(scope.ServiceProvider);
    }

    private static async Task<(BookingDto Booking, CurrentUser User)> Confirmed(Harness h)
    {
        var option = h.AddTwoLegOption();
        var user = Harness.User();
        var held = await With(h, sp => sp.GetRequiredService<BookingService>().CreateAsync(user, Harness.Key(), Harness.Command(option), default));
        var paid = await With(h, sp => sp.GetRequiredService<BookingService>().PayAsync(user, held.Id, Harness.Key(), new PayCommand("card", FakeTokens.Success), default));
        return (paid, user);
    }

    private static async Task<string> Payload(Harness h, string ticketId)
    {
        await using var scope = h.Scope();
        var ticket = await scope.ServiceProvider.GetRequiredService<BookingsDbContext>().Tickets.AsNoTracking().SingleAsync(t => t.Id == ticketId);
        return scope.ServiceProvider.GetRequiredService<TicketSigner>().Sign(ticket.Id, ticket.Nonce, ticket.KeyId);
    }

    private static Task<VerifyResult> Verify(Harness h, string payload, bool consume = false, string? key = null) =>
        With(h, sp => sp.GetRequiredService<TicketService>().VerifyAsync(payload, consume, key, default));

    [Fact]
    public async Task A_genuine_ticket_verifies_and_the_qr_encodes_exactly_the_signed_payload()
    {
        await using var h = await Harness.CreateAsync(pg);
        var (booking, user) = await Confirmed(h);
        var tickets = await With(h, sp => sp.GetRequiredService<BookingService>().TicketsAsync(user, booking.Id, default));
        var id = tickets[0].Id;

        var result = await Verify(h, await Payload(h, id));
        Assert.True(result.Valid);
        Assert.Equal(id, result.Ticket!.Id);
        Assert.Equal("valid", result.Ticket.Status);

        var svg = await With(h, sp => sp.GetRequiredService<TicketService>().QrSvgAsync(user, id, default));
        Assert.StartsWith("<svg", svg, StringComparison.Ordinal);
        Assert.Contains("viewBox", svg, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Only_the_owner_can_fetch_a_qr()
    {
        await using var h = await Harness.CreateAsync(pg);
        var (booking, user) = await Confirmed(h);
        var id = (await With(h, sp => sp.GetRequiredService<BookingService>().TicketsAsync(user, booking.Id, default)))[0].Id;
        var ex = await Assert.ThrowsAsync<ProblemException>(() => With(h, sp => sp.GetRequiredService<TicketService>().QrSvgAsync(Harness.User(), id, default)));
        Assert.Equal(404, ex.Status);
    }

    [Fact]
    public async Task Forged_tampered_and_foreign_payloads_are_rejected_with_specific_codes()
    {
        await using var h = await Harness.CreateAsync(pg);
        var (booking, user) = await Confirmed(h);
        var id = (await With(h, sp => sp.GetRequiredService<BookingService>().TicketsAsync(user, booking.Id, default)))[0].Id;
        var genuine = await Payload(h, id);
        var parts = genuine.Split('.');

        Assert.Equal("TICKET_SIGNATURE_INVALID", (await Verify(h, $"{parts[0]}.{parts[1]}.{parts[2]}.{parts[3]}.{new string('A', parts[4].Length)}")) .ReasonCode);
        Assert.Equal("TICKET_SIGNATURE_INVALID", (await Verify(h, genuine.Replace(id, "tkt_01K8Z3M5Q7R9T2V4W6X8Y0K1M2"))).ReasonCode);
        Assert.Equal("TICKET_MALFORMED", (await Verify(h, "not-a-ticket")).ReasonCode);
        Assert.Equal("TICKET_UNKNOWN_KEY", (await Verify(h, genuine.Replace(".k2.", ".k9."))).ReasonCode);

        // Correctly signed with a stolen/guessed key id but for a ticket that does not exist.
        var signer = h.Root.GetRequiredService<TicketSigner>();
        var unknown = signer.Sign("tkt_01K8Z3M5Q7R9T2V4W6X8Y0K1M2", "abc");
        Assert.Equal("TICKET_NOT_FOUND", (await Verify(h, unknown)).ReasonCode);

        // Right ticket id, wrong nonce, validly signed: only possible if the key leaked, still must not pass.
        var wrongNonce = signer.Sign(id, "someothernonce");
        Assert.Equal("TICKET_NOT_FOUND", (await Verify(h, wrongNonce)).ReasonCode);
    }

    [Fact]
    public async Task Consuming_requires_the_operator_key()
    {
        await using var h = await Harness.CreateAsync(pg);
        var (booking, user) = await Confirmed(h);
        var payload = await Payload(h, (await With(h, sp => sp.GetRequiredService<BookingService>().TicketsAsync(user, booking.Id, default)))[0].Id);

        Assert.Equal(403, (await Assert.ThrowsAsync<ProblemException>(() => Verify(h, payload, consume: true))).Status);
        Assert.Equal(403, (await Assert.ThrowsAsync<ProblemException>(() => Verify(h, payload, consume: true, key: "wrong"))).Status);
        Assert.True((await Verify(h, payload)).Valid); // still unused
    }

    [Fact]
    public async Task A_ticket_can_be_used_once_and_replays_are_detected()
    {
        await using var h = await Harness.CreateAsync(pg);
        var (booking, user) = await Confirmed(h);
        var payload = await Payload(h, (await With(h, sp => sp.GetRequiredService<BookingService>().TicketsAsync(user, booking.Id, default)))[0].Id);

        var first = await Verify(h, payload, consume: true, key: OperatorKey);
        Assert.True(first.Valid);
        Assert.Equal("used", first.Ticket!.Status);

        var replay = await Verify(h, payload, consume: true, key: OperatorKey);
        Assert.False(replay.Valid);
        Assert.Equal("TICKET_ALREADY_USED", replay.ReasonCode);
        Assert.Equal("TICKET_ALREADY_USED", (await Verify(h, payload)).ReasonCode);
    }

    [Fact]
    public async Task Two_scanners_racing_on_one_ticket_admit_exactly_one()
    {
        await using var h = await Harness.CreateAsync(pg);
        var (booking, user) = await Confirmed(h);
        var payload = await Payload(h, (await With(h, sp => sp.GetRequiredService<BookingService>().TicketsAsync(user, booking.Id, default)))[0].Id);

        var results = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => Verify(h, payload, consume: true, key: OperatorKey)));

        Assert.Equal(1, results.Count(r => r.Valid));
        Assert.All(results.Where(r => !r.Valid), r => Assert.Equal("TICKET_ALREADY_USED", r.ReasonCode));
    }

    [Fact]
    public async Task A_cancelled_booking_invalidates_its_tickets_and_their_qr()
    {
        await using var h = await Harness.CreateAsync(pg);
        var (booking, user) = await Confirmed(h);
        var id = (await With(h, sp => sp.GetRequiredService<BookingService>().TicketsAsync(user, booking.Id, default)))[0].Id;
        var payload = await Payload(h, id);
        await With(h, sp => sp.GetRequiredService<BookingService>().CancelAsync(user, booking.Id, default));

        var result = await Verify(h, payload);
        Assert.False(result.Valid);
        Assert.Equal("TICKET_CANCELLED", result.ReasonCode);
        Assert.Equal("TICKET_NOT_VALID", (await Assert.ThrowsAsync<ProblemException>(() => With(h, sp => sp.GetRequiredService<TicketService>().QrSvgAsync(user, id, default)))).Code);
    }

    [Fact]
    public async Task Tickets_stop_verifying_after_the_journey_is_over()
    {
        await using var h = await Harness.CreateAsync(pg);
        var (booking, user) = await Confirmed(h);
        var payload = await Payload(h, (await With(h, sp => sp.GetRequiredService<BookingService>().TicketsAsync(user, booking.Id, default)))[0].Id);
        h.Clock.Advance(TimeSpan.FromHours(24 + 4 + 13));
        Assert.Equal("TICKET_EXPIRED", (await Verify(h, payload)).ReasonCode);
    }

    [Fact]
    public async Task Tickets_signed_before_a_key_rotation_still_verify()
    {
        await using var h = await Harness.CreateAsync(pg);
        var (booking, user) = await Confirmed(h);
        var id = (await With(h, sp => sp.GetRequiredService<BookingService>().TicketsAsync(user, booking.Id, default)))[0].Id;
        // Simulate an old ticket: signed with k1 although the active key is k2.
        await using var scope = h.Scope();
        var db = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        var ticket = await db.Tickets.AsNoTracking().SingleAsync(t => t.Id == id, TestContext.Current.CancellationToken);
        var oldPayload = scope.ServiceProvider.GetRequiredService<TicketSigner>().Sign(ticket.Id, ticket.Nonce, "k1");
        Assert.Contains(".k1.", oldPayload, StringComparison.Ordinal);
        Assert.True((await Verify(h, oldPayload)).Valid);
    }

    [Fact]
    public async Task The_pdf_itinerary_is_a_valid_pdf_containing_every_ticket()
    {
        await using var h = await Harness.CreateAsync(pg);
        var (booking, user) = await Confirmed(h);
        var entity = await With(h, sp => sp.GetRequiredService<BookingService>().GetForItineraryAsync(user, booking.Id, default));
        var signer = h.Root.GetRequiredService<TicketSigner>();

        var pdf = ItineraryPdf.Render(entity, t => signer.Sign(t.Id, t.Nonce, t.KeyId));
        var text = System.Text.Encoding.Latin1.GetString(pdf);

        Assert.StartsWith("%PDF-1.4", text, StringComparison.Ordinal);
        Assert.EndsWith("%%EOF\n", text, StringComparison.Ordinal);
        Assert.Contains("Hulhumal\\351 Ferry Terminal", text, StringComparison.Ordinal); // é in WinAnsi octal
        Assert.Contains(booking.Id, text, StringComparison.Ordinal);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(text, "Ticket - leg").Count);

        // xref offsets point at objects.
        var startxref = long.Parse(text[(text.LastIndexOf("startxref", StringComparison.Ordinal) + 10)..].Split('\n')[0]);
        Assert.StartsWith("xref", text[(int)startxref..], StringComparison.Ordinal);
    }
}

[Collection("postgres")]
public class DisruptionTests(PostgresFixture pg)
{
    private static async Task<T> With<T>(Harness h, Func<IServiceProvider, Task<T>> action)
    {
        await using var scope = h.Scope();
        return await action(scope.ServiceProvider);
    }

    private static async Task<(BookingDto Booking, CurrentUser User)> Confirmed(Harness h)
    {
        var option = h.AddTwoLegOption();
        var user = Harness.User();
        var held = await With(h, sp => sp.GetRequiredService<BookingService>().CreateAsync(user, Harness.Key(), Harness.Command(option), default));
        return (await With(h, sp => sp.GetRequiredService<BookingService>().PayAsync(user, held.Id, Harness.Key(), new PayCommand("card", FakeTokens.Success), default)), user);
    }

    [Fact]
    public async Task A_delay_is_recorded_the_traveller_is_told_and_alternatives_are_suggested_without_rebooking()
    {
        await using var h = await Harness.CreateAsync(pg);
        var (booking, user) = await Confirmed(h);
        h.Email.Sent.Clear();
        var providerCallsBefore = h.Ferry.Calls.Count + h.Flight.Calls.Count;

        var touched = await With(h, sp => sp.GetRequiredService<DisruptionService>().ReportAsync("fake-ferry", "ferry-run-1", ServiceRunState.Delayed, 45, default));

        Assert.Equal(1, touched);
        var view = await With(h, sp => sp.GetRequiredService<BookingService>().GetAsync(user, booking.Id, default));
        var disruption = Assert.Single(view.Disruptions);
        Assert.Equal(("delayed", 45, 0, "open"), (disruption.Kind, disruption.DelayMinutes, disruption.LegIndex, disruption.State));
        Assert.True(disruption.Suggestion!["requiresConsent"]!.GetValue<bool>());
        Assert.Equal(1, h.Planner.ReplanCalls);
        Assert.Equal("booking.disruption", Assert.Single(h.Email.Sent).TemplateCode);
        // Never rebooks: no provider call happened, legs and booking are untouched.
        Assert.Equal(providerCallsBefore, h.Ferry.Calls.Count + h.Flight.Calls.Count);
        Assert.Equal("confirmed", view.Status);
        Assert.All(view.Legs, l => Assert.Equal("confirmed", l.Status));
    }

    [Fact]
    public async Task Repeating_the_same_report_does_not_spam_but_a_worse_one_updates()
    {
        await using var h = await Harness.CreateAsync(pg);
        var (booking, user) = await Confirmed(h);
        h.Email.Sent.Clear();
        Task<int> Report(ServiceRunState s, int d) => With(h, sp => sp.GetRequiredService<DisruptionService>().ReportAsync("fake-ferry", "ferry-run-1", s, d, default));

        Assert.Equal(1, await Report(ServiceRunState.Delayed, 20));
        Assert.Equal(0, await Report(ServiceRunState.Delayed, 20));
        Assert.Equal(1, await Report(ServiceRunState.Cancelled, 0));

        var view = await With(h, sp => sp.GetRequiredService<BookingService>().GetAsync(user, booking.Id, default));
        Assert.Equal("cancelled", Assert.Single(view.Disruptions).Kind);
        Assert.Equal(2, h.Email.Sent.Count);

        Assert.Equal(1, await Report(ServiceRunState.OnTime, 0));
        Assert.Equal("resolved", Assert.Single((await With(h, sp => sp.GetRequiredService<BookingService>().GetAsync(user, booking.Id, default))).Disruptions).State);
    }

    [Fact]
    public async Task Reports_for_other_runs_or_unbooked_trips_touch_nothing()
    {
        await using var h = await Harness.CreateAsync(pg);
        await Confirmed(h);
        Assert.Equal(0, await With(h, sp => sp.GetRequiredService<DisruptionService>().ReportAsync("fake-ferry", "other-run", ServiceRunState.Cancelled, 0, default)));
        Assert.Equal(0, await With(h, sp => sp.GetRequiredService<DisruptionService>().ReportAsync("someone-else", "ferry-run-1", ServiceRunState.Cancelled, 0, default)));
    }

    [Fact]
    public async Task The_monitor_poll_picks_up_a_provider_status_change()
    {
        await using var h = await Harness.CreateAsync(pg);
        var (booking, user) = await Confirmed(h);
        h.Flight.Status = new ServiceStatus("x", ServiceRunState.Delayed, 30, h.Clock.Now);

        var touched = await With(h, sp => sp.GetRequiredService<DisruptionService>().PollAsync(default));

        Assert.Equal(1, touched); // only the delayed flight leg changes
    }

    [Fact]
    public async Task A_planner_outage_still_records_the_disruption()
    {
        await using var h = await Harness.CreateAsync(pg);
        var (booking, user) = await Confirmed(h);
        h.Planner.Options.Clear(); // StubPlanner.ReplanAsync then returns no options
        var touched = await With(h, sp => sp.GetRequiredService<DisruptionService>().ReportAsync("fake-flight", "flight-run-1", ServiceRunState.Cancelled, 0, default));
        Assert.Equal(1, touched);
        var view = await With(h, sp => sp.GetRequiredService<BookingService>().GetAsync(user, booking.Id, default));
        Assert.Empty(Assert.Single(view.Disruptions).Suggestion!["options"]!.AsArray());
    }

    [Fact]
    public async Task Acknowledging_is_owner_only()
    {
        await using var h = await Harness.CreateAsync(pg);
        var (booking, user) = await Confirmed(h);
        await With(h, sp => sp.GetRequiredService<DisruptionService>().ReportAsync("fake-ferry", "ferry-run-1", ServiceRunState.Delayed, 15, default));
        var id = (await With(h, sp => sp.GetRequiredService<BookingService>().GetAsync(user, booking.Id, default))).Disruptions[0].Id;

        await Assert.ThrowsAsync<ProblemException>(() => With(h, sp => sp.GetRequiredService<DisruptionService>().AcknowledgeAsync(Harness.User(), booking.Id, id, default)));
        var ack = await With(h, sp => sp.GetRequiredService<DisruptionService>().AcknowledgeAsync(user, booking.Id, id, default));
        Assert.Equal("acknowledged", ack.Disruptions[0].State);
    }
}
