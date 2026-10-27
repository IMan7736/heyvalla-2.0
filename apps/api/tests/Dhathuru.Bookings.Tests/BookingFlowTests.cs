using Dhathuru.Bookings;
using Dhathuru.Bookings.Domain;
using Dhathuru.Bookings.Payments;
using Dhathuru.Bookings.Services;
using Dhathuru.Domain.Http;
using Dhathuru.Domain.Providers;
using Dhathuru.Domain.Transport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dhathuru.Bookings.Tests;

[Collection("postgres")]
public class BookingFlowTests(PostgresFixture pg)
{
    private static PayCommand Card(string token = FakeTokens.Success) => new("card", token);

    private static async Task<T> With<T>(Harness h, Func<BookingService, Task<T>> action)
    {
        await using var scope = h.Scope();
        return await action(scope.ServiceProvider.GetRequiredService<BookingService>());
    }

    private static async Task<ProblemException> Problem(Func<Task> action) => await Assert.ThrowsAsync<ProblemException>(action);

    // ---------- create / hold ----------

    [Fact]
    public async Task Create_holds_every_bookable_leg_and_totals_the_fares()
    {
        await using var h = await Harness.CreateAsync(pg);
        var option = h.AddTwoLegOption();
        var user = Harness.User();

        var booking = await With(h, s => s.CreateAsync(user, Harness.Key(), Harness.Command(option, adults: 2), default));

        Assert.Equal("held", booking.Status);
        Assert.Equal(9000 + 85000, booking.Total.AmountMinor); // the planner quotes the whole party, so it is charged as quoted
        Assert.All(booking.Legs, l => Assert.Equal("held", l.Status));
        Assert.Equal(h.Clock.Now.AddMinutes(12), DateTimeOffset.Parse(booking.HoldExpiresAt!));
        Assert.Equal(2, h.Ferry.ActiveHolds + h.Flight.ActiveHolds);
        Assert.Equal(98, h.Ferry.SeatsLeft("ferry-run-1"));
    }

    [Fact]
    public async Task Infants_do_not_take_a_seat_or_change_the_quoted_price()
    {
        await using var h = await Harness.CreateAsync(pg);
        var option = h.AddTwoLegOption();
        var booking = await With(h, s => s.CreateAsync(Harness.User(), Harness.Key(), Harness.Command(option, adults: 1, infants: 1), default));
        Assert.Equal(94000, booking.Total.AmountMinor);
        Assert.Equal(99, h.Ferry.SeatsLeft("ferry-run-1"));
        Assert.Equal(2, booking.Passengers.Count);
    }

    [Fact]
    public async Task Create_is_idempotent_and_a_reused_key_with_a_different_body_is_rejected()
    {
        await using var h = await Harness.CreateAsync(pg);
        var option = h.AddTwoLegOption();
        var user = Harness.User();
        var key = Harness.Key();

        var first = await With(h, s => s.CreateAsync(user, key, Harness.Command(option), default));
        var second = await With(h, s => s.CreateAsync(user, key, Harness.Command(option), default));

        Assert.Equal(first.Id, second.Id);
        Assert.Equal(99, h.Ferry.SeatsLeft("ferry-run-1")); // held once, not twice
        var clash = await Problem(() => With(h, s => s.CreateAsync(user, key, Harness.Command(option, adults: 2), default)));
        Assert.Equal("IDEMPOTENCY_KEY_REUSED", clash.Code);
        Assert.Equal(409, clash.Status);
    }

    [Fact]
    public async Task Parallel_creates_with_one_key_produce_one_booking()
    {
        await using var h = await Harness.CreateAsync(pg);
        var option = h.AddTwoLegOption();
        var user = Harness.User();
        var key = Harness.Key();

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ =>
            With(h, async s =>
            {
                try { return (await s.CreateAsync(user, key, Harness.Command(option), default)).Id; }
                catch (ProblemException) { return null; }
            })));

        Assert.Single(results.Where(r => r is not null).Distinct());
        await using var scope = h.Scope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<BookingsDbContext>().Bookings.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_sold_out_second_leg_rolls_back_the_first_and_reports_which_leg()
    {
        await using var h = await Harness.CreateAsync(pg);
        var option = h.AddTwoLegOption();
        h.Flight.SetSeats("flight-run-1", 0);

        var problem = await Problem(() => With(h, s => s.CreateAsync(Harness.User(), Harness.Key(), Harness.Command(option), default)));

        Assert.Equal("LEG_UNAVAILABLE", problem.Code);
        Assert.Equal(1, problem.Params["legIndex"]);
        Assert.Equal(0, h.Ferry.ActiveHolds);
        Assert.Equal(100, h.Ferry.SeatsLeft("ferry-run-1"));
    }

    [Fact]
    public async Task A_failed_create_replays_the_same_error_for_the_same_key()
    {
        await using var h = await Harness.CreateAsync(pg);
        var option = h.AddTwoLegOption();
        h.Flight.SetSeats("flight-run-1", 0);
        var user = Harness.User();
        var key = Harness.Key();

        await Problem(() => With(h, s => s.CreateAsync(user, key, Harness.Command(option), default)));
        var again = await Problem(() => With(h, s => s.CreateAsync(user, key, Harness.Command(option), default)));
        Assert.Equal("LEG_UNAVAILABLE", again.Code);
        Assert.Equal(1, again.Params["legIndex"]);
    }

    [Fact]
    public async Task A_provider_that_throws_is_treated_as_unavailable_not_a_crash()
    {
        await using var h = await Harness.CreateAsync(pg);
        var option = h.AddTwoLegOption();
        h.Flight.FailHold = _ => throw new InvalidOperationException("provider exploded");

        var problem = await Problem(() => With(h, s => s.CreateAsync(Harness.User(), Harness.Key(), Harness.Command(option), default)));
        Assert.Equal("LEG_UNAVAILABLE", problem.Code);
        Assert.Equal(0, h.Ferry.ActiveHolds);
    }

    [Fact]
    public async Task Unknown_option_and_bad_input_are_precise_errors()
    {
        await using var h = await Harness.CreateAsync(pg);
        var user = Harness.User();
        var missing = new CreateBookingCommand(Dhathuru.Domain.Ids.TripOptionId.New().Value, [new PassengerInput(null, "A", "adult", null)]);
        Assert.Equal("OPTION_NOT_FOUND", (await Problem(() => With(h, s => s.CreateAsync(user, Harness.Key(), missing, default)))).Code);

        var option = h.AddTwoLegOption();
        var noAdult = new CreateBookingCommand(option.Id.Value, [new PassengerInput(null, "Kid", "child", null)]);
        var invalid = await Problem(() => With(h, s => s.CreateAsync(user, Harness.Key(), noAdult, default)));
        Assert.Equal(422, invalid.Status);
        Assert.Contains(invalid.Errors, e => e.Code == "ADULT_REQUIRED");

        var tooMany = new CreateBookingCommand(option.Id.Value, Enumerable.Range(0, 10).Select(i => new PassengerInput(null, $"P{i}", "adult", null)).ToList());
        Assert.Contains((await Problem(() => With(h, s => s.CreateAsync(user, Harness.Key(), tooMany, default)))).Errors, e => e.Code == "PASSENGER_COUNT");

        var infants = new CreateBookingCommand(option.Id.Value, [new PassengerInput(null, "A", "adult", null), new PassengerInput(null, "I1", "infant", null), new PassengerInput(null, "I2", "infant", null)]);
        Assert.Contains((await Problem(() => With(h, s => s.CreateAsync(user, Harness.Key(), infants, default)))).Errors, e => e.Code == "TOO_MANY_INFANTS");
    }

    [Fact]
    public async Task A_departed_option_cannot_be_booked()
    {
        await using var h = await Harness.CreateAsync(pg);
        var option = h.AddTwoLegOption();
        h.Clock.Advance(TimeSpan.FromHours(25));
        Assert.Equal("OPTION_EXPIRED", (await Problem(() => With(h, s => s.CreateAsync(Harness.User(), Harness.Key(), Harness.Command(option), default)))).Code);
    }

    [Fact]
    public async Task Estimated_taxi_legs_are_shown_but_not_charged()
    {
        await using var h = await Harness.CreateAsync(pg);
        var depart = h.Clock.Now.AddHours(24);
        var option = h.Option(
            Harness.Leg(TransportMode.Taxi, "taxi", "n/a", depart, depart.AddMinutes(20), 12000, bookable: false),
            Harness.Leg(TransportMode.Ferry, "fake-ferry", "ferry-run-1", depart.AddMinutes(60), depart.AddMinutes(150), 9000));
        h.Planner.Options[option.Id.Value] = option;

        var booking = await With(h, s => s.CreateAsync(Harness.User(), Harness.Key(), Harness.Command(option), default));

        Assert.Equal(9000, booking.Total.AmountMinor);
        Assert.Contains(booking.PriceBreakdown, l => l.PayOnBoard && l.Amount.AmountMinor == 12000);
        Assert.Equal(1, h.Ferry.ActiveHolds);
    }

    // ---------- ownership (IDOR) ----------

    [Fact]
    public async Task Another_traveller_cannot_read_pay_cancel_or_list_a_booking()
    {
        await using var h = await Harness.CreateAsync(pg);
        var option = h.AddTwoLegOption();
        var owner = Harness.User();
        var stranger = Harness.User();
        var booking = await With(h, s => s.CreateAsync(owner, Harness.Key(), Harness.Command(option), default));

        Assert.Equal(404, (await Problem(() => With(h, s => s.GetAsync(stranger, booking.Id, default)))).Status);
        Assert.Equal(404, (await Problem(() => With(h, s => s.PayAsync(stranger, booking.Id, Harness.Key(), Card(), default)))).Status);
        Assert.Equal(404, (await Problem(() => With(h, s => s.CancelAsync(stranger, booking.Id, default)))).Status);
        Assert.Equal(404, (await Problem(() => With(h, s => s.TicketsAsync(stranger, booking.Id, default)))).Status);
        Assert.Equal(404, (await Problem(() => With(h, s => s.QuoteCancellationAsync(stranger, booking.Id, default)))).Status);
        Assert.Empty((await With(h, s => s.ListAsync(stranger, null, 50, null, default))).Items);
        Assert.Single((await With(h, s => s.ListAsync(owner, null, 50, null, default))).Items);
        Assert.Equal("held", (await With(h, s => s.GetAsync(owner, booking.Id, default))).Status);
    }

    [Fact]
    public async Task Malformed_ids_are_not_found_rather_than_errors()
    {
        await using var h = await Harness.CreateAsync(pg);
        Assert.Equal(404, (await Problem(() => With(h, s => s.GetAsync(Harness.User(), "bkg_nope' OR 1=1 --", default)))).Status);
    }

    [Fact]
    public async Task Listing_pages_newest_first_with_a_cursor_and_filters_by_status()
    {
        await using var h = await Harness.CreateAsync(pg);
        var user = Harness.User();
        var ids = new List<string>();
        for (var i = 0; i < 5; i++)
        {
            var option = h.AddTwoLegOption(ferryRun: $"fr{i}", flightRun: $"fl{i}");
            ids.Add((await With(h, s => s.CreateAsync(user, Harness.Key(), Harness.Command(option), default))).Id);
            h.Clock.Advance(TimeSpan.FromMinutes(1));
        }

        var page1 = await With(h, s => s.ListAsync(user, null, 2, null, default));
        var page2 = await With(h, s => s.ListAsync(user, page1.Page.NextCursor, 2, null, default));
        var page3 = await With(h, s => s.ListAsync(user, page2.Page.NextCursor, 2, null, default));

        var seen = page1.Items.Concat(page2.Items).Concat(page3.Items).Select(b => b.Id).ToList();
        Assert.Equal(Enumerable.Reverse(ids), seen);
        Assert.Null(page3.Page.NextCursor);
        Assert.Empty((await With(h, s => s.ListAsync(user, null, 10, BookingStatus.Confirmed, default))).Items);
        Assert.Equal(422, (await Problem(() => With(h, s => s.ListAsync(user, "!!!", 10, null, default)))).Status);
    }

    // ---------- payment ----------

    [Fact]
    public async Task Successful_payment_confirms_all_legs_issues_one_ticket_per_leg_and_passenger_and_emails()
    {
        await using var h = await Harness.CreateAsync(pg);
        var option = h.AddTwoLegOption();
        var user = Harness.User();
        var held = await With(h, s => s.CreateAsync(user, Harness.Key(), Harness.Command(option, adults: 2), default));

        var paid = await With(h, s => s.PayAsync(user, held.Id, Harness.Key(), Card(), default));

        Assert.Equal("confirmed", paid.Status);
        Assert.Null(paid.HoldExpiresAt);
        Assert.All(paid.Legs, l => Assert.Equal("confirmed", l.Status));
        Assert.All(paid.Legs, l => Assert.StartsWith("P-", l.ProviderReference));
        Assert.Equal("succeeded", paid.Payment!.Status);
        Assert.Equal(paid.Total, paid.Payment.Amount);
        Assert.Equal(h.Gateway.CapturedMinor(await GatewayRef(h, held.Id)), paid.Total.AmountMinor);

        var tickets = await With(h, s => s.TicketsAsync(user, held.Id, default));
        Assert.Equal(4, tickets.Count);
        Assert.All(tickets, t => Assert.Equal("valid", t.Status));
        Assert.Equal(4, tickets.Select(t => (t.BookingLegId, t.PassengerId)).Distinct().Count());

        var mail = Assert.Single(h.Email.Sent);
        Assert.Equal("booking.confirmed", mail.TemplateCode);
        var pdf = Assert.Single(mail.Attachments!);
        Assert.Equal("application/pdf", pdf.ContentType);
        Assert.StartsWith("%PDF-1.4", System.Text.Encoding.Latin1.GetString(pdf.Content.Span[..8]));
    }

    private static async Task<string> GatewayRef(Harness h, string bookingId)
    {
        await using var scope = h.Scope();
        var db = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        return (await db.Payments.SingleAsync(p => p.BookingId == bookingId)).GatewayReference!;
    }

    [Fact]
    public async Task Paying_twice_with_the_same_key_charges_once()
    {
        await using var h = await Harness.CreateAsync(pg);
        var option = h.AddTwoLegOption();
        var user = Harness.User();
        var held = await With(h, s => s.CreateAsync(user, Harness.Key(), Harness.Command(option), default));
        var key = Harness.Key();

        var first = await With(h, s => s.PayAsync(user, held.Id, key, Card(), default));
        var retry = await With(h, s => s.PayAsync(user, held.Id, key, Card(), default));

        Assert.Equal("confirmed", retry.Status);
        Assert.Equal(first.Payment!.Id, retry.Payment!.Id);
        Assert.Equal(2, h.Ferry.ConfirmedCount + h.Flight.ConfirmedCount);
        await using var scope = h.Scope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<BookingsDbContext>().Payments.CountAsync(TestContext.Current.CancellationToken));
        Assert.Single(h.Email.Sent);
    }

    [Fact]
    public async Task Two_different_keys_racing_to_pay_charge_exactly_once()
    {
        await using var h = await Harness.CreateAsync(pg, c => { c["Bookings:FakeGateway:SlowDelay"] = "00:00:00.300"; c["Bookings:PaymentTimeout"] = "00:00:10"; });
        var option = h.AddTwoLegOption();
        var user = Harness.User();
        var held = await With(h, s => s.CreateAsync(user, Harness.Key(), Harness.Command(option), default));

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => With(h, async s =>
        {
            try { return (await s.PayAsync(user, held.Id, Harness.Key(), Card(FakeTokens.Slow), default)).Status; }
            catch (ProblemException ex) { return ex.Code; }
        })));

        Assert.Equal(1, outcomes.Count(o => o == "confirmed"));
        Assert.All(outcomes.Where(o => o != "confirmed"), o => Assert.Equal("PAYMENT_IN_PROGRESS", o));
        await using var scope = h.Scope();
        var db = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        Assert.Equal(1, await db.Payments.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, h.Ferry.ConfirmedCount + h.Flight.ConfirmedCount);
        Assert.Equal(1, await db.Tickets.CountAsync(t => t.Status == TicketStatus.Valid, TestContext.Current.CancellationToken) / 2);
    }

    [Theory]
    [InlineData(FakeTokens.Declined, PaymentFailureCodes.CardDeclined)]
    [InlineData(FakeTokens.InsufficientFunds, PaymentFailureCodes.InsufficientFunds)]
    [InlineData("tok_garbage", PaymentFailureCodes.InvalidToken)]
    public async Task A_declined_card_keeps_the_hold_so_the_traveller_can_try_another_card(string token, string code)
    {
        await using var h = await Harness.CreateAsync(pg);
        var option = h.AddTwoLegOption();
        var user = Harness.User();
        var held = await With(h, s => s.CreateAsync(user, Harness.Key(), Harness.Command(option), default));

        var declined = await With(h, s => s.PayAsync(user, held.Id, Harness.Key(), Card(token), default));
        Assert.Equal("held", declined.Status);
        Assert.Equal("failed", declined.Payment!.Status);
        Assert.Equal(code, declined.Payment.FailureCode);
        Assert.NotNull(declined.HoldExpiresAt);
        Assert.Equal(2, h.Ferry.ActiveHolds + h.Flight.ActiveHolds);
        Assert.Empty(h.Email.Sent);

        var retried = await With(h, s => s.PayAsync(user, held.Id, Harness.Key(), Card(), default));
        Assert.Equal("confirmed", retried.Status);
        Assert.Equal("succeeded", retried.Payment!.Status);
    }

    [Fact]
    public async Task Verification_step_confirms_with_the_right_code()
    {
        await using var h = await Harness.CreateAsync(pg);
        var option = h.AddTwoLegOption();
        var user = Harness.User();
        var held = await With(h, s => s.CreateAsync(user, Harness.Key(), Harness.Command(option), default));

        var pending = await With(h, s => s.PayAsync(user, held.Id, Harness.Key(), Card(FakeTokens.Verification), default));
        Assert.Equal("payment_pending", pending.Status);
        Assert.Equal("requires_action", pending.Payment!.Status);
        Assert.NotNull(pending.Payment.Challenge);
        Assert.All(pending.Legs, l => Assert.Equal("held", l.Status));

        var done = await With(h, s => s.CompleteChallengeAsync(user, held.Id, FakeTokens.VerificationCode, default));
        Assert.Equal("confirmed", done.Status);
        Assert.Equal("succeeded", done.Payment!.Status);
        Assert.Null(done.Payment.Challenge);
    }

    [Fact]
    public async Task Wrong_verification_code_returns_to_held_with_a_precise_failure()
    {
        await using var h = await Harness.CreateAsync(pg);
        var option = h.AddTwoLegOption();
        var user = Harness.User();
        var held = await With(h, s => s.CreateAsync(user, Harness.Key(), Harness.Command(option), default));
        await With(h, s => s.PayAsync(user, held.Id, Harness.Key(), Card(FakeTokens.Verification), default));

        var failed = await With(h, s => s.CompleteChallengeAsync(user, held.Id, "000000", default));

        Assert.Equal("held", failed.Status);
        Assert.Equal(PaymentFailureCodes.VerificationFailed, failed.Payment!.FailureCode);
        Assert.Equal("NO_VERIFICATION_PENDING", (await Problem(() => With(h, s => s.CompleteChallengeAsync(user, held.Id, FakeTokens.VerificationCode, default)))).Code);
    }

    [Fact]
    public async Task A_gateway_timeout_where_nothing_was_charged_returns_to_held()
    {
        await using var h = await Harness.CreateAsync(pg);
        var option = h.AddTwoLegOption();
        var user = Harness.User();
        var held = await With(h, s => s.CreateAsync(user, Harness.Key(), Harness.Command(option), default));

        var result = await With(h, s => s.PayAsync(user, held.Id, Harness.Key(), Card(FakeTokens.Timeout), default));

        Assert.Equal("held", result.Status);
        Assert.Equal(PaymentFailureCodes.PaymentTimeout, result.Payment!.FailureCode);
        Assert.Equal(0, h.Ferry.ConfirmedCount);
    }

    [Fact]
    public async Task A_lost_response_after_a_real_charge_is_reconciled_and_the_trip_is_confirmed()
    {
        await using var h = await Harness.CreateAsync(pg);
        var option = h.AddTwoLegOption();
        var user = Harness.User();
        var held = await With(h, s => s.CreateAsync(user, Harness.Key(), Harness.Command(option), default));

        var result = await With(h, s => s.PayAsync(user, held.Id, Harness.Key(), Card(FakeTokens.TimeoutCharged), default));

        // The customer was charged, so they must not be left without a booking.
        Assert.Equal("confirmed", result.Status);
        Assert.Equal("succeeded", result.Payment!.Status);
        Assert.Equal(2, h.Ferry.ConfirmedCount + h.Flight.ConfirmedCount);
    }

    [Fact]
    public async Task Pay_validates_the_request()
    {
        await using var h = await Harness.CreateAsync(pg);
        var option = h.AddTwoLegOption();
        var user = Harness.User();
        var held = await With(h, s => s.CreateAsync(user, Harness.Key(), Harness.Command(option), default));
        Assert.Equal(422, (await Problem(() => With(h, s => s.PayAsync(user, held.Id, Harness.Key(), new PayCommand("cash", "x"), default)))).Status);
        Assert.Equal(422, (await Problem(() => With(h, s => s.PayAsync(user, held.Id, Harness.Key(), new PayCommand("card", ""), default)))).Status);
    }

    [Fact]
    public async Task Paying_an_already_confirmed_booking_with_a_new_key_is_a_conflict_not_a_second_charge()
    {
        await using var h = await Harness.CreateAsync(pg);
        var option = h.AddTwoLegOption();
        var user = Harness.User();
        var held = await With(h, s => s.CreateAsync(user, Harness.Key(), Harness.Command(option), default));
        await With(h, s => s.PayAsync(user, held.Id, Harness.Key(), Card(), default));

        var problem = await Problem(() => With(h, s => s.PayAsync(user, held.Id, Harness.Key(), Card(), default)));
        Assert.Equal("BOOKING_NOT_PAYABLE", problem.Code);
        await using var scope = h.Scope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<BookingsDbContext>().Payments.CountAsync(TestContext.Current.CancellationToken));
    }

    // ---------- saga compensation ----------

    [Fact]
    public async Task When_the_second_leg_fails_to_confirm_everything_is_undone_and_the_customer_is_fully_refunded()
    {
        await using var h = await Harness.CreateAsync(pg);
        var option = h.AddTwoLegOption();
        var user = Harness.User();
        var held = await With(h, s => s.CreateAsync(user, Harness.Key(), Harness.Command(option, adults: 2), default));
        h.Flight.FailConfirm = _ => true;

        var result = await With(h, s => s.PayAsync(user, held.Id, Harness.Key(), Card(), default));

        Assert.Equal("failed", result.Status);
        Assert.Equal("LEG_CONFIRM_FAILED", result.FailureCode);
        Assert.Equal("refunded", result.Payment!.Status);
        Assert.Equal(result.Total, result.Payment.Refunded);
        var ferryLeg = result.Legs.Single(l => l.LegIndex == 0);
        var flightLeg = result.Legs.Single(l => l.LegIndex == 1);
        Assert.Equal("released", ferryLeg.Status);
        Assert.Equal("failed", flightLeg.Status);
        Assert.Equal("PROVIDER_REJECTED", flightLeg.FailureCode);
        Assert.Equal(0, h.Ferry.ActiveHolds);
        Assert.Equal(0, h.Ferry.ConfirmedCount);
        Assert.Equal(100, h.Ferry.SeatsLeft("ferry-run-1"));
        Assert.Equal(100, h.Flight.SeatsLeft("flight-run-1"));
        var gatewayRef = await GatewayRef(h, held.Id);
        Assert.Equal(h.Gateway.CapturedMinor(gatewayRef), h.Gateway.RefundedMinor(gatewayRef));
        Assert.Empty(await With(h, s => s.TicketsAsync(user, held.Id, default)));
        Assert.Equal("booking.failed", Assert.Single(h.Email.Sent).TemplateCode);
    }

    [Fact]
    public async Task A_provider_that_throws_during_confirm_triggers_the_same_compensation()
    {
        await using var h = await Harness.CreateAsync(pg);
        var option = h.AddTwoLegOption();
        var user = Harness.User();
        var held = await With(h, s => s.CreateAsync(user, Harness.Key(), Harness.Command(option), default));
        h.Flight.FailConfirm = _ => throw new InvalidOperationException("boom");

        var result = await With(h, s => s.PayAsync(user, held.Id, Harness.Key(), Card(), default));

        Assert.Equal("failed", result.Status);
        Assert.Equal("PROVIDER_ERROR", result.Legs.Single(l => l.LegIndex == 1).FailureCode);
        Assert.Equal("refunded", result.Payment!.Status);
    }

    [Fact]
    public async Task The_fake_leg_failure_token_exercises_the_saga_end_to_end_when_simulation_is_on()
    {
        await using var h = await Harness.CreateAsync(pg, simulation: true);
        var option = h.AddTwoLegOption();
        var user = Harness.User();
        var held = await With(h, s => s.CreateAsync(user, Harness.Key(), Harness.Command(option), default));

        var result = await With(h, s => s.PayAsync(user, held.Id, Harness.Key(), Card(FakeTokens.LegFailure), default));

        Assert.Equal("failed", result.Status);
        Assert.Equal("SIMULATED_LEG_FAILURE", result.Legs.Single(l => l.LegIndex == 1).FailureCode);
        Assert.Equal("refunded", result.Payment!.Status);
        Assert.Equal(0, h.Ferry.ConfirmedCount);
    }

    [Fact]
    public async Task The_leg_failure_token_does_nothing_when_simulation_is_off()
    {
        await using var h = await Harness.CreateAsync(pg, simulation: false);
        var option = h.AddTwoLegOption();
        var user = Harness.User();
        var held = await With(h, s => s.CreateAsync(user, Harness.Key(), Harness.Command(option), default));
        Assert.Equal("confirmed", (await With(h, s => s.PayAsync(user, held.Id, Harness.Key(), Card(FakeTokens.LegFailure), default))).Status);
    }

    [Fact]
    public async Task If_the_provider_cannot_cancel_during_compensation_the_reconciler_finishes_it_later()
    {
        await using var h = await Harness.CreateAsync(pg);
        var option = h.AddTwoLegOption();
        var user = Harness.User();
        var held = await With(h, s => s.CreateAsync(user, Harness.Key(), Harness.Command(option), default));
        h.Flight.FailConfirm = _ => true;
        h.Ferry.FailCancel = _ => true; // ferry is down while we try to undo

        var result = await With(h, s => s.PayAsync(user, held.Id, Harness.Key(), Card(), default));

        Assert.Equal("failed", result.Status);
        Assert.Equal("RELEASE_PENDING", result.Legs.Single(l => l.LegIndex == 0).FailureCode); // still owed a release, and says so
        Assert.Equal("refunded", result.Payment!.Status);                       // money was returned regardless
        Assert.Equal(99, h.Ferry.SeatsLeft("ferry-run-1"));

        h.Ferry.FailCancel = _ => false;
        Assert.True(await With(h, s => s.ReconcileAsync(default)) > 0);

        var healed = await With(h, s => s.GetAsync(user, held.Id, default));
        Assert.Equal("released", healed.Legs.Single(l => l.LegIndex == 0).Status);
        Assert.Null(healed.Legs.Single(l => l.LegIndex == 0).FailureCode);
        Assert.Equal(100, h.Ferry.SeatsLeft("ferry-run-1"));
        Assert.Equal(0, await With(h, s => s.ReconcileAsync(default)));
    }

    [Fact]
    public async Task If_the_refund_fails_during_compensation_it_is_retried_and_never_duplicated()
    {
        await using var h = await Harness.CreateAsync(pg);
        var option = h.AddTwoLegOption();
        var user = Harness.User();
        var held = await With(h, s => s.CreateAsync(user, Harness.Key(), Harness.Command(option), default));
        h.Flight.FailConfirm = _ => true;
        h.Flaky.FailRefunds = true;

        var result = await With(h, s => s.PayAsync(user, held.Id, Harness.Key(), Card(), default));

        Assert.Equal("failed", result.Status);
        Assert.Equal("succeeded", result.Payment!.Status); // money still with us until the refund lands
        Assert.Equal(100, h.Ferry.SeatsLeft("ferry-run-1")); // seats were released regardless

        h.Flaky.FailRefunds = false;
        Assert.True(await With(h, s => s.ReconcileAsync(default)) > 0);
        await With(h, s => s.ReconcileAsync(default));
        await With(h, s => s.ReconcileAsync(default));

        var healed = await With(h, s => s.GetAsync(user, held.Id, default));
        Assert.Equal("refunded", healed.Payment!.Status);
        var gatewayRef = await GatewayRef(h, held.Id);
        Assert.Equal(h.Gateway.CapturedMinor(gatewayRef), h.Gateway.RefundedMinor(gatewayRef));
        await using var scope = h.Scope();
        Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<BookingsDbContext>().Refunds.CountAsync(r => r.Status == RefundStatus.Succeeded, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_cancellation_whose_refund_failed_is_repaired_by_the_sweeper()
    {
        await using var h = await Harness.CreateAsync(pg);
        var depart = h.Clock.Now.AddDays(10);
        var option = h.Option(Harness.Leg(TransportMode.Ferry, "fake-ferry", "ferry-run-1", depart, depart.AddHours(2), 9000));
        h.Planner.Options[option.Id.Value] = option;
        var user = Harness.User();
        var held = await With(h, s => s.CreateAsync(user, Harness.Key(), Harness.Command(option), default));
        await With(h, s => s.PayAsync(user, held.Id, Harness.Key(), Card(), default));
        h.Flaky.FailRefunds = true;

        var cancelled = await With(h, s => s.CancelAsync(user, held.Id, default));
        Assert.Equal("cancelled", cancelled.Status);
        Assert.Equal("succeeded", cancelled.Payment!.Status); // not refunded yet, and honestly reported

        h.Flaky.FailRefunds = false;
        Assert.Equal(1, await With(h, s => s.ReconcileAsync(default)));
        var healed = await With(h, s => s.GetAsync(user, held.Id, default));
        Assert.Equal("refunded", healed.Status);
        Assert.Equal("refunded", healed.Payment!.Status);
        Assert.Equal(0, await With(h, s => s.ReconcileAsync(default)));
        Assert.Equal(9000, h.Gateway.RefundedMinor(await GatewayRef(h, held.Id)));
    }

    // ---------- expiry ----------

    [Fact]
    public async Task The_sweeper_expires_a_stale_hold_and_returns_the_seats()
    {
        await using var h = await Harness.CreateAsync(pg);
        var option = h.AddTwoLegOption();
        var user = Harness.User();
        var held = await With(h, s => s.CreateAsync(user, Harness.Key(), Harness.Command(option, adults: 3), default));
        Assert.Equal(97, h.Ferry.SeatsLeft("ferry-run-1"));

        h.Clock.Advance(TimeSpan.FromMinutes(11));
        Assert.Equal(0, await With(h, s => s.ReconcileAsync(default)));
        h.Clock.Advance(TimeSpan.FromMinutes(2));
        Assert.Equal(1, await With(h, s => s.ReconcileAsync(default)));

        var expired = await With(h, s => s.GetAsync(user, held.Id, default));
        Assert.Equal("expired", expired.Status);
        Assert.All(expired.Legs, l => Assert.Equal("released", l.Status));
        Assert.Equal(100, h.Ferry.SeatsLeft("ferry-run-1"));
        Assert.Equal(100, h.Flight.SeatsLeft("flight-run-1"));
    }

    [Fact]
    public async Task Paying_after_the_hold_ran_out_is_gone_410_and_releases_the_seats_without_charging()
    {
        await using var h = await Harness.CreateAsync(pg);
        var option = h.AddTwoLegOption();
        var user = Harness.User();
        var held = await With(h, s => s.CreateAsync(user, Harness.Key(), Harness.Command(option), default));
        h.Clock.Advance(TimeSpan.FromMinutes(13));

        var problem = await Problem(() => With(h, s => s.PayAsync(user, held.Id, Harness.Key(), Card(), default)));

        Assert.Equal(410, problem.Status);
        Assert.Equal("HOLD_EXPIRED", problem.Code);
        Assert.Equal("expired", (await With(h, s => s.GetAsync(user, held.Id, default))).Status);
        Assert.Equal(100, h.Ferry.SeatsLeft("ferry-run-1"));
        await using var scope = h.Scope();
        Assert.Equal(0, await scope.ServiceProvider.GetRequiredService<BookingsDbContext>().Payments.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal("HOLD_EXPIRED", (await Problem(() => With(h, s => s.PayAsync(user, held.Id, Harness.Key(), Card(), default)))).Code);
    }

    [Fact]
    public async Task A_declined_card_after_the_hold_lapsed_expires_the_booking()
    {
        await using var h = await Harness.CreateAsync(pg, c => c["Bookings:FakeGateway:SlowDelay"] = "00:00:00.050");
        var option = h.AddTwoLegOption();
        var user = Harness.User();
        var held = await With(h, s => s.CreateAsync(user, Harness.Key(), Harness.Command(option), default));
        await With(h, s => s.PayAsync(user, held.Id, Harness.Key(), Card(FakeTokens.Verification), default));
        h.Clock.Advance(TimeSpan.FromMinutes(13));

        var failed = await With(h, s => s.CompleteChallengeAsync(user, held.Id, "000000", default));

        Assert.Equal("expired", failed.Status);
        Assert.Equal(100, h.Ferry.SeatsLeft("ferry-run-1"));
    }

    [Fact]
    public async Task Cancelling_before_paying_releases_the_seats()
    {
        await using var h = await Harness.CreateAsync(pg);
        var option = h.AddTwoLegOption();
        var user = Harness.User();
        var held = await With(h, s => s.CreateAsync(user, Harness.Key(), Harness.Command(option), default));

        var cancelled = await With(h, s => s.CancelAsync(user, held.Id, default));

        Assert.Equal("cancelled", cancelled.Status);
        Assert.Equal(100, h.Ferry.SeatsLeft("ferry-run-1"));
        Assert.Equal("cancelled", (await With(h, s => s.CancelAsync(user, held.Id, default))).Status);
    }

    // ---------- inventory under contention ----------

    [Fact]
    public async Task Concurrent_bookings_for_the_last_seats_never_oversell()
    {
        await using var h = await Harness.CreateAsync(pg);
        h.Ferry.SetSeats("ferry-run-1", 3);
        var option = h.AddTwoLegOption();

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => With(h, async s =>
        {
            try { await s.CreateAsync(Harness.User(), Harness.Key(), Harness.Command(option), default); return "ok"; }
            catch (ProblemException ex) { return ex.Code; }
        })));

        Assert.Equal(3, outcomes.Count(o => o == "ok"));
        Assert.All(outcomes.Where(o => o != "ok"), o => Assert.Equal("LEG_UNAVAILABLE", o));
        Assert.Equal(0, h.Ferry.SeatsLeft("ferry-run-1"));
        // Every booking that lost the race returned its flight seat too.
        Assert.Equal(97, h.Flight.SeatsLeft("flight-run-1"));
    }

    // ---------- cancellation and refunds ----------

    [Fact]
    public async Task Cancelling_a_confirmed_trip_refunds_by_each_legs_policy()
    {
        await using var h = await Harness.CreateAsync(pg);
        var option = h.AddTwoLegOption(); // ferry departs in 24h (100%), flight in 26.5h (flight fee 5000 keeps)
        var user = Harness.User();
        var held = await With(h, s => s.CreateAsync(user, Harness.Key(), Harness.Command(option), default));
        await With(h, s => s.PayAsync(user, held.Id, Harness.Key(), Card(), default));

        var quote = await With(h, s => s.QuoteCancellationAsync(user, held.Id, default));
        Assert.True(quote.Cancellable);
        Assert.Equal(9000, quote.Legs.Single(l => l.LegIndex == 0).Refund.AmountMinor);
        Assert.Equal(42500 - 5000, quote.Legs.Single(l => l.LegIndex == 1).Refund.AmountMinor); // 50% tier (<72h) less fee

        var cancelled = await With(h, s => s.CancelAsync(user, held.Id, default));

        Assert.Equal("cancelled", cancelled.Status);
        Assert.Equal(9000 + 37500, cancelled.Payment!.Refunded!.AmountMinor);
        Assert.Equal("partially_refunded", cancelled.Payment.Status);
        Assert.All(cancelled.Legs, l => Assert.Equal("cancelled", l.Status));
        Assert.All(await With(h, s => s.TicketsAsync(user, held.Id, default)), t => Assert.Equal("cancelled", t.Status));
        Assert.Equal(100, h.Ferry.SeatsLeft("ferry-run-1"));
        var gatewayRef = await GatewayRef(h, held.Id);
        Assert.Equal(46500, h.Gateway.RefundedMinor(gatewayRef));
        Assert.Contains(h.Email.Sent, m => m.TemplateCode == "booking.cancelled");

        Assert.Equal("cancelled", (await With(h, s => s.CancelAsync(user, held.Id, default))).Status);
        Assert.Equal(46500, h.Gateway.RefundedMinor(gatewayRef)); // idempotent: not refunded twice
    }

    [Fact]
    public async Task A_fully_refundable_cancellation_ends_in_refunded()
    {
        await using var h = await Harness.CreateAsync(pg);
        var depart = h.Clock.Now.AddDays(10);
        var option = h.Option(Harness.Leg(TransportMode.Ferry, "fake-ferry", "ferry-run-1", depart, depart.AddHours(2), 9000));
        h.Planner.Options[option.Id.Value] = option;
        var user = Harness.User();
        var held = await With(h, s => s.CreateAsync(user, Harness.Key(), Harness.Command(option), default));
        await With(h, s => s.PayAsync(user, held.Id, Harness.Key(), Card(), default));

        var cancelled = await With(h, s => s.CancelAsync(user, held.Id, default));

        Assert.Equal("refunded", cancelled.Status);
        Assert.Equal("refunded", cancelled.Payment!.Status);
    }

    [Fact]
    public async Task A_departed_leg_stays_booked_and_the_booking_becomes_partially_cancelled()
    {
        await using var h = await Harness.CreateAsync(pg);
        var option = h.AddTwoLegOption();
        var user = Harness.User();
        var held = await With(h, s => s.CreateAsync(user, Harness.Key(), Harness.Command(option), default));
        await With(h, s => s.PayAsync(user, held.Id, Harness.Key(), Card(), default));
        h.Clock.Advance(TimeSpan.FromHours(24) + TimeSpan.FromMinutes(30)); // ferry has left, flight has not

        var cancelled = await With(h, s => s.CancelAsync(user, held.Id, default));

        Assert.Equal("partially_cancelled", cancelled.Status);
        Assert.Equal("confirmed", cancelled.Legs.Single(l => l.LegIndex == 0).Status);
        Assert.Equal("cancelled", cancelled.Legs.Single(l => l.LegIndex == 1).Status);
        var tickets = await With(h, s => s.TicketsAsync(user, held.Id, default));
        Assert.Equal("valid", tickets.Single(t => t.Leg!["mode"]!.GetValue<string>() == "ferry").Status);
    }

    [Fact]
    public async Task Everything_departed_means_nothing_to_cancel()
    {
        await using var h = await Harness.CreateAsync(pg);
        var option = h.AddTwoLegOption();
        var user = Harness.User();
        var held = await With(h, s => s.CreateAsync(user, Harness.Key(), Harness.Command(option), default));
        await With(h, s => s.PayAsync(user, held.Id, Harness.Key(), Card(), default));
        h.Clock.Advance(TimeSpan.FromHours(30));

        Assert.False((await With(h, s => s.QuoteCancellationAsync(user, held.Id, default))).Cancellable);
        Assert.Equal("NOTHING_CANCELLABLE", (await Problem(() => With(h, s => s.CancelAsync(user, held.Id, default)))).Code);
    }

    [Fact]
    public async Task A_provider_that_refuses_to_cancel_one_leg_leaves_it_booked_and_refunds_only_the_rest()
    {
        await using var h = await Harness.CreateAsync(pg);
        var option = h.AddTwoLegOption();
        var user = Harness.User();
        var held = await With(h, s => s.CreateAsync(user, Harness.Key(), Harness.Command(option), default));
        await With(h, s => s.PayAsync(user, held.Id, Harness.Key(), Card(), default));
        h.Flight.FailCancel = _ => true;

        var cancelled = await With(h, s => s.CancelAsync(user, held.Id, default));

        Assert.Equal("partially_cancelled", cancelled.Status);
        Assert.Equal("confirmed", cancelled.Legs.Single(l => l.LegIndex == 1).Status);
        Assert.Equal("PROVIDER_DOWN", cancelled.Legs.Single(l => l.LegIndex == 1).FailureCode);
        Assert.Equal(9000, cancelled.Payment!.Refunded!.AmountMinor);
        Assert.Equal("CANCEL_FAILED", (await Problem(() => With(h, s => s.CancelAsync(user, held.Id, default)))).Code);

        h.Flight.FailCancel = _ => false;
        var retry = await With(h, s => s.CancelAsync(user, held.Id, default));
        Assert.Equal("cancelled", retry.Status);
        Assert.Equal(9000 + 37500, retry.Payment!.Refunded!.AmountMinor);
        Assert.Null(retry.Legs.Single(l => l.LegIndex == 1).FailureCode);
    }

    [Fact]
    public async Task A_failed_or_expired_booking_cannot_be_cancelled()
    {
        await using var h = await Harness.CreateAsync(pg);
        var option = h.AddTwoLegOption();
        var user = Harness.User();
        var held = await With(h, s => s.CreateAsync(user, Harness.Key(), Harness.Command(option), default));
        h.Clock.Advance(TimeSpan.FromMinutes(20));
        await With(h, s => s.ReconcileAsync(default));
        Assert.Equal("BOOKING_NOT_CANCELLABLE", (await Problem(() => With(h, s => s.CancelAsync(user, held.Id, default)))).Code);
    }

    // ---------- email failures never change outcomes ----------

    [Fact]
    public async Task A_mail_outage_does_not_undo_a_confirmed_booking()
    {
        await using var h = await Harness.CreateAsync(pg);
        h.Email.Throw = true;
        var option = h.AddTwoLegOption();
        var user = Harness.User();
        var held = await With(h, s => s.CreateAsync(user, Harness.Key(), Harness.Command(option), default));
        Assert.Equal("confirmed", (await With(h, s => s.PayAsync(user, held.Id, Harness.Key(), Card(), default))).Status);
    }
}

[Collection("postgres")]
public class AccountDeletionTests(PostgresFixture pg)
{
    [Fact]
    public async Task Deleting_an_account_anonymises_its_bookings_but_keeps_them()
    {
        await using var h = await Harness.CreateAsync(pg);
        var option = h.AddTwoLegOption();
        var user = Harness.User();
        BookingDto booking;
        await using (var scope = h.Scope())
        {
            var s = scope.ServiceProvider.GetRequiredService<BookingService>();
            booking = await s.CreateAsync(user, Harness.Key(), Harness.Command(option), TestContext.Current.CancellationToken);
            await s.PayAsync(user, booking.Id, Harness.Key(), new PayCommand("card", FakeTokens.Success), TestContext.Current.CancellationToken);
        }

        await using (var scope = h.Scope())
        {
            var handler = scope.ServiceProvider.GetRequiredService<Dhathuru.Domain.Seams.IAccountDeletionHandler>();
            await handler.OnAccountDeletingAsync(user.Id, TestContext.Current.CancellationToken);
        }

        await using var check = h.Scope();
        var db = check.ServiceProvider.GetRequiredService<BookingsDbContext>();
        var stored = await db.Bookings.Include(b => b.Passengers).SingleAsync(b => b.Id == booking.Id, TestContext.Current.CancellationToken);
        Assert.NotEqual(user.Id, stored.UserId);
        Assert.Null(stored.ContactEmail);
        Assert.All(stored.Passengers, p => Assert.Equal("Deleted traveller", p.FullName));
        Assert.Equal(BookingStatus.Confirmed, stored.Status);
        await Problem(() => check.ServiceProvider.GetRequiredService<BookingService>().GetAsync(user, booking.Id, default));
    }

    private static async Task<ProblemException> Problem(Func<Task> action) => await Assert.ThrowsAsync<ProblemException>(action);
}
