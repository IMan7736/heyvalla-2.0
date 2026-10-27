using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dhathuru.Bookings.Domain;
using Dhathuru.Bookings.Payments;
using Dhathuru.Bookings.Policies;
using Dhathuru.Bookings.Tickets;
using Dhathuru.Domain.Http;
using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Pricing;
using Dhathuru.Domain.Providers;
using Dhathuru.Domain.Seams;
using IPaymentGateway = Dhathuru.Bookings.Payments.IPaymentGateway;
using RefundRequest = Dhathuru.Bookings.Payments.RefundRequest;
using Dhathuru.Domain.Transport;
using Dhathuru.Domain.Trips;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Dhathuru.Bookings.Services;

public sealed record PassengerInput(string? TravellerId, string FullName, string PassengerType, string? Phone);

public sealed record CreateBookingCommand(string OptionId, IReadOnlyList<PassengerInput> Passengers);

public sealed record PayCommand(string Method, string PaymentToken);

/// <summary>
/// Booking use cases. Money and seats stay consistent: every provider call has an idempotency key derived from the
/// booking, every state change goes through <see cref="BookingStateMachine"/>, and every failure path either completes
/// its compensation or leaves a durable record the reconciler finishes (see <see cref="ReconcileAsync"/>).
/// </summary>
public sealed class BookingService(
    BookingsDbContext db,
    ITripPlanner planner,
    ProviderRegistry providers,
    IPaymentGateway gateway,
    ICancellationPolicyCatalog policies,
    TicketSigner signer,
    BookingNotifier notifier,
    IConfirmFaultInjector faults,
    TimeProvider clock,
    IOptions<BookingsOptions> options,
    ILogger<BookingService> logger)
{
    private const string SystemActor = "system";
    private readonly BookingsOptions _o = options.Value;

    // ---------- create / hold ----------

    public async Task<BookingDto> CreateAsync(CurrentUser user, string idempotencyKey, CreateBookingCommand command, CancellationToken ct)
    {
        var errors = Validate(command);
        if (errors.Count > 0)
        {
            throw ProblemException.Validation([.. errors]);
        }

        var hash = Hash(command);
        var existing = await Load(db.Bookings.Where(b => b.UserId == user.Id && b.CreateKey == idempotencyKey)).SingleOrDefaultAsync(ct);
        if (existing is not null)
        {
            return ReplayCreate(existing, hash);
        }

        if (!TripOptionId.TryParse(command.OptionId, out var optionId))
        {
            throw ProblemException.Validation(new FieldError("optionId", "INVALID_ID"));
        }

        var option = await planner.GetOptionAsync(optionId, ct) ?? throw ProblemException.NotFound("OPTION_NOT_FOUND");
        var now = clock.GetUtcNow();
        if (option.Legs.Count == 0 || option.Legs.Any(l => l.Bookable && l.DepartAt <= now))
        {
            throw ProblemException.Conflict("OPTION_EXPIRED");
        }

        var passengers = command.Passengers;
        var payers = passengers.Count(p => p.PassengerType != "infant");
        var seats = payers;
        var bookingId = BookingId.New().Value;

        var lines = new List<(string Code, long AmountMinor, int? LegIndex, bool PayOnBoard)>();
        long total = 0;
        var legs = new List<BookingLeg>();
        for (var i = 0; i < option.Legs.Count; i++)
        {
            var leg = option.Legs[i];
            if (leg.Price.Currency != Money.Mvr)
            {
                throw ProblemException.Conflict("CURRENCY_UNSUPPORTED");
            }
            // The planner prices each leg for the whole party it searched for (taxis per vehicle), so it is charged as quoted.
            var amount = leg.Price.AmountMinor;
            // Only legs we hold and charge count towards the total. Taxi estimates and walks are paid on board or free.
            var charged = leg.Bookable;
            if (charged)
            {
                total = checked(total + amount);
            }
            lines.Add((leg.Bookable ? "LEG_FARE" : (leg.Pricing == PricingKind.Estimate ? "LEG_ESTIMATE" : "LEG_FARE"), amount, i, !leg.Bookable));
            legs.Add(new BookingLeg(BookingLegId.New().Value, bookingId, i, LegSnapshots.ToJson(leg).ToJsonString(), leg.ProviderId, leg.ServiceRunId, leg.Bookable, charged ? amount : 0));
        }

        if (_o.ServiceFeeMinor > 0)
        {
            total = checked(total + _o.ServiceFeeMinor);
            lines.Add(("SERVICE_FEE", _o.ServiceFeeMinor, null, false));
        }

        if (legs.All(l => !l.Bookable))
        {
            throw ProblemException.Conflict("NOTHING_TO_BOOK");
        }

        var booking = Booking.Create(bookingId, user.Id, command.OptionId, new Money(total), BookingViews.PriceLinesJson(lines),
            user.Email, user.Locale, idempotencyKey, hash, now);
        foreach (var leg in legs)
        {
            booking.AddLeg(leg);
        }
        foreach (var p in passengers)
        {
            booking.AddPassenger(new BookingPassenger(PassengerId.New().Value, bookingId, p.FullName.Trim(),
                Enum.Parse<PassengerKind>(p.PassengerType, ignoreCase: true), p.TravellerId, p.Phone));
        }

        db.Bookings.Add(booking);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A concurrent request with the same key may have won the insert: replay it, otherwise this is a real error.
            db.ChangeTracker.Clear();
            var winner = await Load(db.Bookings.Where(b => b.UserId == user.Id && b.CreateKey == idempotencyKey)).SingleOrDefaultAsync(ct);
            if (winner is null)
            {
                throw;
            }
            return ReplayCreate(winner, hash);
        }

        // Hold every bookable leg in order; the first failure rolls the rest back.
        var holdFor = _o.HoldTtl + _o.ProviderHoldGrace;
        var expiresAt = now + _o.HoldTtl;
        foreach (var leg in booking.Legs.Where(l => l.Bookable).OrderBy(l => l.LegIndex))
        {
            var (ok, reference, providerExpires, code) = await HoldLegAsync(leg, seats, holdFor, ct);
            if (!ok)
            {
                leg.MarkFailed(code ?? "HOLD_FAILED");
                await ReleaseLegsAsync(booking, ct);
                booking.Transition(BookingStatus.Failed, clock.GetUtcNow(), SystemActor, "hold_failed", "LEG_UNAVAILABLE");
                await db.SaveChangesAsync(ct);
                throw ProblemException.Conflict("LEG_UNAVAILABLE", new Dictionary<string, object?> { ["legIndex"] = leg.LegIndex });
            }

            leg.MarkHeld(reference!);
            if (providerExpires is { } pe && pe < expiresAt)
            {
                expiresAt = pe;
            }
            await db.SaveChangesAsync(ct);
        }

        booking.StartHold(expiresAt, clock.GetUtcNow(), SystemActor);
        await db.SaveChangesAsync(ct);
        return BookingViews.ToDto(booking);
    }

    private BookingDto ReplayCreate(Booking existing, string hash)
    {
        if (existing.CreateRequestHash != hash)
        {
            throw ProblemException.Conflict("IDEMPOTENCY_KEY_REUSED");
        }

        if (existing.Status == BookingStatus.Failed && existing.FailureCode == "LEG_UNAVAILABLE")
        {
            var failed = existing.Legs.FirstOrDefault(l => l.Status == BookingLegStatus.Failed);
            throw ProblemException.Conflict("LEG_UNAVAILABLE", new Dictionary<string, object?> { ["legIndex"] = failed?.LegIndex ?? 0 });
        }

        return BookingViews.ToDto(existing);
    }

    private async Task<(bool Ok, string? Reference, DateTimeOffset? ExpiresAt, string? Code)> HoldLegAsync(BookingLeg leg, int seats, TimeSpan holdFor, CancellationToken ct)
    {
        var provider = providers.Find(leg.ProviderId);
        if (provider is null || leg.ServiceRunId is null)
        {
            return (false, null, null, "PROVIDER_UNAVAILABLE");
        }

        try
        {
            var result = await provider.HoldAsync(new HoldRequest(leg.ServiceRunId, seats, $"{leg.BookingId}:{leg.LegIndex}:hold", holdFor), ct);
            return result.Success && result.HoldReference is not null
                ? (true, result.HoldReference, result.ExpiresAt, null)
                : (false, null, null, result.FailureCode ?? "HOLD_FAILED");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Hold failed for {BookingId} leg {Index}.", leg.BookingId, leg.LegIndex);
            return (false, null, null, "PROVIDER_ERROR");
        }
    }

    // ---------- reads ----------

    public async Task<BookingDto> GetAsync(CurrentUser user, string bookingId, CancellationToken ct) =>
        BookingViews.ToDto(await LoadOwned(user, bookingId, ct));

    public async Task<BookingPageDto> ListAsync(CurrentUser user, string? cursor, int limit, BookingStatus? status, CancellationToken ct)
    {
        limit = Math.Clamp(limit, 1, 100);
        var query = Load(db.Bookings.AsNoTracking().Where(b => b.UserId == user.Id));
        if (status is { } s)
        {
            query = query.Where(b => b.Status == s);
        }

        if (cursor is not null)
        {
            if (!TryDecodeCursor(cursor, out var at, out var id))
            {
                throw ProblemException.Validation(new FieldError("cursor", "INVALID_CURSOR"));
            }
            query = query.Where(b => b.CreatedAt < at || (b.CreatedAt == at && string.Compare(b.Id, id) < 0));
        }

        var items = await query.OrderByDescending(b => b.CreatedAt).ThenByDescending(b => b.Id).Take(limit + 1).ToListAsync(ct);
        var next = items.Count > limit ? EncodeCursor(items[limit - 1]) : null;
        return new BookingPageDto([.. items.Take(limit).Select(BookingViews.ToDto)], new PageInfoDto(next, limit));
    }

    public async Task<IReadOnlyList<TicketDto>> TicketsAsync(CurrentUser user, string bookingId, CancellationToken ct)
    {
        var booking = await LoadOwned(user, bookingId, ct, track: false);
        var legs = booking.Legs.ToDictionary(l => l.Id);
        return [.. booking.Tickets.OrderBy(t => legs[t.BookingLegId].LegIndex).ThenBy(t => t.Id).Select(t => BookingViews.ToDto(t, legs[t.BookingLegId]))];
    }

    public async Task<Booking> GetForItineraryAsync(CurrentUser user, string bookingId, CancellationToken ct) =>
        await LoadOwned(user, bookingId, ct, track: false);

    // ---------- pay (saga) ----------

    public async Task<BookingDto> PayAsync(CurrentUser user, string bookingId, string idempotencyKey, PayCommand command, CancellationToken ct)
    {
        if (command.Method != "card" || string.IsNullOrWhiteSpace(command.PaymentToken) || command.PaymentToken.Length > 128)
        {
            throw ProblemException.Validation(new FieldError("paymentToken", "INVALID_PAYMENT_TOKEN"));
        }

        var booking = await LoadOwned(user, bookingId, ct);
        var now = clock.GetUtcNow();

        // Replay of the same request: report where it ended up.
        if (booking.Payments.Any(p => p.IdempotencyKey == idempotencyKey))
        {
            return BookingViews.ToDto(booking);
        }

        if (booking.Status == BookingStatus.Held && booking.IsHoldExpired(now))
        {
            await ExpireAsync(booking, ct);
            throw new ProblemException(StatusCodes.Status410Gone, "HOLD_EXPIRED");
        }

        if (booking.Status == BookingStatus.Expired)
        {
            throw new ProblemException(StatusCodes.Status410Gone, "HOLD_EXPIRED");
        }

        if (booking.Status == BookingStatus.PaymentPending)
        {
            throw ProblemException.Conflict("PAYMENT_IN_PROGRESS");
        }

        if (booking.Status != BookingStatus.Held)
        {
            throw ProblemException.Conflict("BOOKING_NOT_PAYABLE", new Dictionary<string, object?> { ["status"] = WireNames.Of(booking.Status) });
        }

        // Claim the booking. The row version means only one concurrent request gets past this point.
        var payment = new PaymentRecord(PaymentId.New().Value, booking.Id, booking.Payments.Count + 1, booking.TotalMinor, booking.Currency, gateway.GatewayId, idempotencyKey, now);
        booking.AddPayment(payment);
        booking.Transition(BookingStatus.PaymentPending, now, user.Id, "payment_started");
        try
        {
            db.Entry(payment).State = EntityState.Added;
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            throw ProblemException.Conflict("PAYMENT_IN_PROGRESS");
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            throw ProblemException.Conflict("PAYMENT_IN_PROGRESS");
        }

        ChargeResult? result;
        try
        {
            result = await ChargeWithTimeoutAsync(booking, payment, command.PaymentToken, ct);
        }
        catch (PaymentOutcomeUnknownException)
        {
            // Gateway unreachable and lookup failed too: stay payment_pending. The reconciler settles it.
            booking.Note("payment_unknown", clock.GetUtcNow(), SystemActor);
            await db.SaveChangesAsync(CancellationToken.None);
            return BookingViews.ToDto(booking);
        }

        await ApplyChargeResultAsync(booking, payment, result, command.PaymentToken, ct);
        return BookingViews.ToDto(booking);
    }

    public async Task<BookingDto> CompleteChallengeAsync(CurrentUser user, string bookingId, string code, CancellationToken ct)
    {
        var booking = await LoadOwned(user, bookingId, ct);
        var payment = booking.Payments.FirstOrDefault(p => p.Status == PaymentStatus.RequiresAction);
        if (booking.Status != BookingStatus.PaymentPending || payment is null || payment.ChallengeId is null || payment.GatewayReference is null)
        {
            throw ProblemException.Conflict("NO_VERIFICATION_PENDING");
        }

        var challengeId = payment.ChallengeId;
        var reference = payment.GatewayReference;
        booking.Note("verification_submitted", clock.GetUtcNow(), user.Id);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            throw ProblemException.Conflict("PAYMENT_IN_PROGRESS");
        }

        ChargeResult result;
        try
        {
            result = await gateway.CompleteChallengeAsync(new CompleteChallengeRequest(reference, challengeId, code), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Verification call failed for {BookingId}.", booking.Id);
            result = new ChargeResult(ChargeOutcome.Declined, FailureCode: PaymentFailureCodes.GatewayError);
        }

        await ApplyChargeResultAsync(booking, payment, result, null, ct);
        return BookingViews.ToDto(booking);
    }

    private async Task<ChargeResult> ChargeWithTimeoutAsync(Booking booking, PaymentRecord payment, string token, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(_o.PaymentTimeout);
        var request = new ChargeRequest(booking.Id, booking.Total, token, payment.IdempotencyKey);
        try
        {
            return await gateway.ChargeAsync(request, timeout.Token);
        }
        catch (Exception ex) when (ex is OperationCanceledException && !ct.IsCancellationRequested || ex is not OperationCanceledException)
        {
            logger.LogWarning("Charge for {BookingId} did not return ({Reason}); looking it up.", booking.Id, ex.GetType().Name);
            try
            {
                var found = await gateway.LookupAsync(payment.IdempotencyKey, CancellationToken.None);
                return found ?? new ChargeResult(ChargeOutcome.Declined, FailureCode: PaymentFailureCodes.PaymentTimeout);
            }
            catch (Exception lookupFailure) when (lookupFailure is not OperationCanceledException)
            {
                throw new PaymentOutcomeUnknownException(lookupFailure);
            }
        }
    }

    private async Task ApplyChargeResultAsync(Booking booking, PaymentRecord payment, ChargeResult result, string? token, CancellationToken ct)
    {
        switch (result.Outcome)
        {
            case ChargeOutcome.RequiresAction:
                payment.RequireAction(result.ChallengeId!, result.GatewayReference!);
                booking.Note("verification_required", clock.GetUtcNow(), SystemActor);
                await db.SaveChangesAsync(CancellationToken.None);
                return;

            case ChargeOutcome.Declined:
                payment.Fail(result.FailureCode ?? PaymentFailureCodes.CardDeclined);
                await ReturnToHeldOrExpireAsync(booking, "payment_failed");
                return;

            default:
                payment.Succeed(result.GatewayReference!);
                booking.Note("payment_succeeded", clock.GetUtcNow(), SystemActor);
                await db.SaveChangesAsync(CancellationToken.None);
                await FinalizeAsync(booking, payment, token, ct);
                return;
        }
    }

    private async Task ReturnToHeldOrExpireAsync(Booking booking, string note)
    {
        var now = clock.GetUtcNow();
        if (booking.IsHoldExpired(now))
        {
            await ExpireAsync(booking, CancellationToken.None);
            return;
        }

        booking.Transition(BookingStatus.Held, now, SystemActor, note);
        await db.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>
    /// Money is in; confirm every bookable leg. Safe to call again after a crash: provider calls are idempotent per leg.
    /// Any leg that cannot be confirmed triggers compensation: cancel the confirmed legs, refund in full, mark the booking failed.
    /// </summary>
    private async Task FinalizeAsync(Booking booking, PaymentRecord payment, string? token, CancellationToken ct)
    {
        _ = ct; // Money is already taken: finishing must not be abandoned by a client disconnect.
        var token0 = CancellationToken.None;
        foreach (var leg in booking.Legs.Where(l => l.Bookable && l.Status != BookingLegStatus.Confirmed).OrderBy(l => l.LegIndex))
        {
            var failure = await ConfirmLegAsync(booking, leg, token, token0);
            if (failure is not null)
            {
                leg.MarkFailed(failure);
                await CompensateAsync(booking, payment, token0);
                return;
            }
            await db.SaveChangesAsync(token0);
        }

        var now = clock.GetUtcNow();
        booking.Transition(BookingStatus.Confirmed, now, SystemActor, "confirmed");
        IssueTickets(booking, now);
        await db.SaveChangesAsync(token0);
        await notifier.ConfirmedAsync(booking, token0);
    }

    private async Task<string?> ConfirmLegAsync(Booking booking, BookingLeg leg, string? token, CancellationToken ct)
    {
        if (leg.HoldReference is null)
        {
            return "HOLD_MISSING";
        }
        if (faults.ShouldFailConfirm(booking, leg, token))
        {
            return "SIMULATED_LEG_FAILURE";
        }

        var provider = providers.Find(leg.ProviderId);
        if (provider is null)
        {
            return "PROVIDER_UNAVAILABLE";
        }

        try
        {
            var result = await provider.ConfirmAsync(new ConfirmRequest(leg.HoldReference, $"{booking.Id}:{leg.LegIndex}:confirm"), ct);
            if (!result.Success || result.ProviderReference is null)
            {
                return result.FailureCode ?? "CONFIRM_FAILED";
            }
            leg.MarkConfirmed(result.ProviderReference);
            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Confirm failed for {BookingId} leg {Index}.", booking.Id, leg.LegIndex);
            return "PROVIDER_ERROR";
        }
    }

    private async Task CompensateAsync(Booking booking, PaymentRecord payment, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        booking.Transition(BookingStatus.Failed, now, SystemActor, "leg_confirm_failed", "LEG_CONFIRM_FAILED");
        await db.SaveChangesAsync(ct);
        await CompensateRemainingAsync(booking, payment, ct);
        await notifier.FailedAsync(booking, ct);
    }

    /// <summary>Cancel/release every leg that still holds a seat and refund anything not yet refunded. Idempotent; retried by the reconciler.</summary>
    private async Task CompensateRemainingAsync(Booking booking, PaymentRecord? payment, CancellationToken ct)
    {
        foreach (var leg in booking.Legs.Where(l => l.NeedsRelease))
        {
            var reference = leg.Status == BookingLegStatus.Confirmed ? leg.ProviderReference : leg.HoldReference;
            if (reference is null || providers.Find(leg.ProviderId) is not { } provider)
            {
                continue;
            }

            try
            {
                var result = await provider.CancelAsync(new CancelRequest(reference, $"{booking.Id}:{leg.LegIndex}:compensate"), ct);
                if (result.Success)
                {
                    leg.MarkReleased();
                }
                else
                {
                    leg.MarkReleasePending();
                    logger.LogError("Provider refused to cancel {BookingId} leg {Index}: {Code}. Reconciler will retry.", booking.Id, leg.LegIndex, result.FailureCode);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                leg.MarkReleasePending();
                logger.LogError(ex, "Provider cancel failed for {BookingId} leg {Index}. Reconciler will retry.", booking.Id, leg.LegIndex);
            }
        }

        if (payment is { Status: PaymentStatus.Succeeded or PaymentStatus.PartiallyRefunded, GatewayReference: not null })
        {
            await RefundAsync(booking, payment, payment.AmountMinor - payment.RefundedMinor, "BOOKING_FAILED", $"{booking.Id}:compensate-refund", ct);
        }

        await db.SaveChangesAsync(ct);
    }

    private async Task<bool> RefundAsync(Booking booking, PaymentRecord payment, long amountMinor, string reason, string key, CancellationToken ct)
    {
        if (amountMinor <= 0)
        {
            return true;
        }

        var refund = await db.Refunds.SingleOrDefaultAsync(r => r.IdempotencyKey == key, ct);
        if (refund is null)
        {
            refund = new RefundRecord(Ulid.NewString(), booking.Id, payment.Id, amountMinor, payment.Currency, reason, key, clock.GetUtcNow());
            db.Refunds.Add(refund);
            await db.SaveChangesAsync(ct);
        }
        else if (refund.Status == RefundStatus.Succeeded)
        {
            return true;
        }

        try
        {
            var outcome = await gateway.RefundAsync(new RefundRequest(payment.GatewayReference!, new Money(refund.AmountMinor, payment.Currency), key), ct);
            if (outcome.Success)
            {
                refund.Succeed();
                payment.AddRefund(refund.AmountMinor);
                return true;
            }
            refund.Fail(outcome.FailureCode ?? PaymentFailureCodes.GatewayError);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Refund call failed for {BookingId}. Reconciler will retry.", booking.Id);
        }
        return false;
    }

    private void IssueTickets(Booking booking, DateTimeOffset now)
    {
        foreach (var leg in booking.Legs.Where(l => l.Bookable && l.Status == BookingLegStatus.Confirmed))
        {
            foreach (var passenger in booking.Passengers)
            {
                if (booking.Tickets.Any(t => t.BookingLegId == leg.Id && t.PassengerId == passenger.Id))
                {
                    continue;
                }
                booking.AddTicket(new Ticket(TicketId.New().Value, booking.Id, leg.Id, passenger.Id, TicketSigner.NewNonce(), signer.ActiveKeyId, now));
            }
        }
    }

    // ---------- expiry and release ----------

    private async Task ReleaseLegsAsync(Booking booking, CancellationToken ct)
    {
        foreach (var leg in booking.Legs.Where(l => l.NeedsRelease && l.Status != BookingLegStatus.Confirmed))
        {
            if (leg.HoldReference is null || providers.Find(leg.ProviderId) is not { } provider)
            {
                continue;
            }

            try
            {
                var result = await provider.CancelAsync(new CancelRequest(leg.HoldReference, $"{booking.Id}:{leg.LegIndex}:release"), ct);
                if (result.Success)
                {
                    leg.MarkReleased();
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Could not release {BookingId} leg {Index}; reconciler will retry.", booking.Id, leg.LegIndex);
            }
        }
    }

    private async Task ExpireAsync(Booking booking, CancellationToken ct)
    {
        try
        {
            booking.Transition(BookingStatus.Expired, clock.GetUtcNow(), SystemActor, "hold_expired", "HOLD_EXPIRED");
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return; // someone else (payment, sweeper) moved it first
        }

        await ReleaseLegsAsync(booking, ct);
        await db.SaveChangesAsync(ct);
    }

    // ---------- cancel ----------

    public async Task<CancellationQuoteDto> QuoteCancellationAsync(CurrentUser user, string bookingId, CancellationToken ct) =>
        BuildQuote(await LoadOwned(user, bookingId, ct, track: false), clock.GetUtcNow());

    private CancellationQuoteDto BuildQuote(Booking booking, DateTimeOffset now)
    {
        if (booking.Status is BookingStatus.Held)
        {
            return new CancellationQuoteDto(booking.Id, true, [], MoneyDto.Of(0, booking.Currency), MoneyDto.Of(0, booking.Currency), null);
        }

        if (booking.Status is not (BookingStatus.Confirmed or BookingStatus.PartiallyCancelled))
        {
            return new CancellationQuoteDto(booking.Id, false, [], MoneyDto.Of(0, booking.Currency), MoneyDto.Of(0, booking.Currency), "BOOKING_NOT_CANCELLABLE");
        }

        var quotes = new List<LegQuoteDto>();
        long refund = 0, nonRefundable = 0;
        foreach (var leg in booking.Legs.Where(l => l.Bookable && l.Status == BookingLegStatus.Confirmed).OrderBy(l => l.LegIndex))
        {
            var json = System.Text.Json.Nodes.JsonNode.Parse(leg.LegJson)!.AsObject();
            LegSnapshots.TryParseMode(json["mode"]!.GetValue<string>(), out var mode);
            var quote = RefundCalculator.Quote(leg.LegIndex, policies.For(leg.ProviderId, mode), leg.AmountMinor, LegSnapshots.DepartAt(json), now);
            quotes.Add(new LegQuoteDto(quote.LegIndex, quote.Cancellable, quote.PolicyCode, quote.RefundPercent, MoneyDto.Of(quote.Refund), quote.ReasonCode));
            if (quote.Cancellable)
            {
                refund += quote.Refund.AmountMinor;
                nonRefundable += leg.AmountMinor - quote.Refund.AmountMinor;
            }
        }

        var any = quotes.Any(q => q.Cancellable);
        return new CancellationQuoteDto(booking.Id, any, quotes, MoneyDto.Of(refund, booking.Currency), MoneyDto.Of(nonRefundable, booking.Currency), any ? null : "NOTHING_CANCELLABLE");
    }

    public async Task<BookingDto> CancelAsync(CurrentUser user, string bookingId, CancellationToken ct)
    {
        var booking = await LoadOwned(user, bookingId, ct);
        var now = clock.GetUtcNow();

        if (booking.Status == BookingStatus.Held)
        {
            booking.Transition(BookingStatus.Cancelled, now, user.Id, "cancelled_before_payment");
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateConcurrencyException)
            {
                db.ChangeTracker.Clear();
                throw ProblemException.Conflict("PAYMENT_IN_PROGRESS");
            }
            await ReleaseLegsAsync(booking, CancellationToken.None);
            await db.SaveChangesAsync(CancellationToken.None);
            return BookingViews.ToDto(booking);
        }

        if (booking.Status == BookingStatus.Cancelled)
        {
            return BookingViews.ToDto(booking); // idempotent
        }

        if (booking.Status is not (BookingStatus.Confirmed or BookingStatus.PartiallyCancelled))
        {
            throw ProblemException.Conflict("BOOKING_NOT_CANCELLABLE", new Dictionary<string, object?> { ["status"] = WireNames.Of(booking.Status) });
        }

        var payment = booking.Payments.Single(p => p.Status is PaymentStatus.Succeeded or PaymentStatus.PartiallyRefunded);
        booking.Note("cancel_requested", now, user.Id);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            throw ProblemException.Conflict("CANCEL_IN_PROGRESS");
        }

        long refundTotal = 0;
        var cancelledAny = false;
        var attempted = 0;
        foreach (var leg in booking.Legs.Where(l => l.Bookable && l.Status == BookingLegStatus.Confirmed).OrderBy(l => l.LegIndex).ToList())
        {
            var json = System.Text.Json.Nodes.JsonNode.Parse(leg.LegJson)!.AsObject();
            LegSnapshots.TryParseMode(json["mode"]!.GetValue<string>(), out var mode);
            var quote = RefundCalculator.Quote(leg.LegIndex, policies.For(leg.ProviderId, mode), leg.AmountMinor, LegSnapshots.DepartAt(json), now);
            if (!quote.Cancellable || providers.Find(leg.ProviderId) is not { } provider || leg.ProviderReference is null)
            {
                continue;
            }

            attempted++;
            try
            {
                var result = await provider.CancelAsync(new CancelRequest(leg.ProviderReference, $"{booking.Id}:{leg.LegIndex}:cancel"), CancellationToken.None);
                if (!result.Success)
                {
                    leg.MarkFailedCancel(result.FailureCode ?? "PROVIDER_REFUSED_CANCEL");
                    continue;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Cancel failed for {BookingId} leg {Index}.", booking.Id, leg.LegIndex);
                leg.MarkFailedCancel("PROVIDER_ERROR");
                continue;
            }

            leg.MarkCancelled(quote.Refund.AmountMinor);
            refundTotal += quote.Refund.AmountMinor;
            cancelledAny = true;
            foreach (var ticket in booking.Tickets.Where(t => t.BookingLegId == leg.Id))
            {
                ticket.Cancel();
            }
            await db.SaveChangesAsync(CancellationToken.None);
        }

        var remaining = booking.Legs.Any(l => l.Bookable && l.Status == BookingLegStatus.Confirmed);
        if (!cancelledAny)
        {
            await db.SaveChangesAsync(CancellationToken.None);
            // Nothing was cancellable at all, or every provider refused: the second case can simply be retried later.
            throw ProblemException.Conflict(attempted > 0 ? "CANCEL_FAILED" : "NOTHING_CANCELLABLE");
        }

        booking.Transition(remaining ? BookingStatus.PartiallyCancelled : BookingStatus.Cancelled, clock.GetUtcNow(), user.Id, "cancelled");
        await db.SaveChangesAsync(CancellationToken.None);

        await SettleCancellationRefundAsync(booking, payment, CancellationToken.None);

        await notifier.CancelledAsync(booking, refundTotal, CancellationToken.None);
        return BookingViews.ToDto(booking);
    }

    /// <summary>
    /// Refunds whatever cancelled legs are owed and not yet returned. The amount comes from the legs, not from the request that
    /// cancelled them, so a crash between "provider cancelled" and "refund sent" is repaired by the next call or sweep.
    /// </summary>
    private async Task SettleCancellationRefundAsync(Booking booking, PaymentRecord payment, CancellationToken ct)
    {
        var owed = booking.Legs.Where(l => l.Status == BookingLegStatus.Cancelled).Sum(l => l.RefundedMinor) - payment.RefundedMinor;
        if (owed > 0 && payment.GatewayReference is not null)
        {
            await RefundAsync(booking, payment, owed, "USER_CANCELLED", $"{booking.Id}:cancel-refund:{payment.RefundedMinor}", ct);
        }

        if (booking.Status == BookingStatus.Cancelled && payment.Status == PaymentStatus.Refunded)
        {
            booking.Transition(BookingStatus.Refunded, clock.GetUtcNow(), SystemActor, "fully_refunded");
        }

        await db.SaveChangesAsync(ct);
    }

    // ---------- reconciliation (sweeper) ----------

    /// <summary>
    /// Finishes anything a crash, timeout or provider outage left half done: expires stale holds, settles unknown payments,
    /// retries compensation and refunds. Returns how many bookings were touched. Safe to run concurrently with requests.
    /// </summary>
    public async Task<int> ReconcileAsync(CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var touched = 0;

        var expired = await Load(db.Bookings.Where(b => b.Status == BookingStatus.Held && b.HoldExpiresAt != null && b.HoldExpiresAt <= now)).OrderBy(b => b.HoldExpiresAt).ThenBy(b => b.Id).Take(50).ToListAsync(ct);
        foreach (var booking in expired)
        {
            await ExpireAsync(booking, ct);
            touched++;
        }

        var stale = await Load(db.Bookings.Where(b => b.Status == BookingStatus.PaymentPending)).OrderBy(b => b.UpdatedAt).ThenBy(b => b.Id).Take(50).ToListAsync(ct);
        foreach (var booking in stale)
        {
            var payment = booking.CurrentPayment;
            if (payment is null)
            {
                continue;
            }

            if (payment.Status == PaymentStatus.Succeeded)
            {
                await FinalizeAsync(booking, payment, null, ct);
                touched++;
            }
            else if (payment.Status == PaymentStatus.Pending && now - payment.CreatedAt > _o.PaymentTimeout)
            {
                ChargeResult? found = null;
                try
                {
                    found = await gateway.LookupAsync(payment.IdempotencyKey, ct);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Lookup failed for {BookingId}; will retry.", booking.Id);
                    continue;
                }

                if (found is { Outcome: ChargeOutcome.Succeeded })
                {
                    payment.Succeed(found.GatewayReference!);
                    await db.SaveChangesAsync(ct);
                    await FinalizeAsync(booking, payment, null, ct);
                }
                else
                {
                    payment.Fail(found?.FailureCode ?? PaymentFailureCodes.PaymentTimeout);
                    await ReturnToHeldOrExpireAsync(booking, "payment_timeout");
                }
                touched++;
            }
            else if (payment.Status == PaymentStatus.RequiresAction && booking.HoldExpiresAt is { } until && until + _o.PaymentPendingGrace <= now)
            {
                payment.Fail(PaymentFailureCodes.VerificationFailed);
                await ExpireAsync(booking, ct);
                touched++;
            }
        }

        var failedOrExpired = await Load(db.Bookings.Where(b => b.Status == BookingStatus.Failed || b.Status == BookingStatus.Expired)).OrderBy(b => b.UpdatedAt).ThenBy(b => b.Id).Take(50).ToListAsync(ct);
        foreach (var booking in failedOrExpired)
        {
            var legsOpen = booking.Legs.Any(l => l.NeedsRelease);
            var payment = booking.Payments.FirstOrDefault(p => p.Status is PaymentStatus.Succeeded or PaymentStatus.PartiallyRefunded);
            if (legsOpen || payment is not null)
            {
                await CompensateRemainingAsync(booking, payment, ct);
                touched++;
            }
        }

        var cancelled = await Load(db.Bookings.Where(b => b.Status == BookingStatus.Cancelled || b.Status == BookingStatus.PartiallyCancelled)).OrderBy(b => b.UpdatedAt).ThenBy(b => b.Id).Take(50).ToListAsync(ct);
        foreach (var booking in cancelled)
        {
            var payment = booking.Payments.FirstOrDefault(p => p.Status is PaymentStatus.Succeeded or PaymentStatus.PartiallyRefunded);
            if (payment is not null && booking.Legs.Where(l => l.Status == BookingLegStatus.Cancelled).Sum(l => l.RefundedMinor) > payment.RefundedMinor)
            {
                await SettleCancellationRefundAsync(booking, payment, ct);
                touched++;
            }
        }

        return touched;
    }

    // ---------- helpers ----------

    private IQueryable<Booking> Load(IQueryable<Booking> source) =>
        source.Include(b => b.Legs).Include(b => b.Passengers).Include(b => b.Payments).Include(b => b.Tickets).Include(b => b.Disruptions).AsSplitQuery();

    private async Task<Booking> LoadOwned(CurrentUser user, string bookingId, CancellationToken ct, bool track = true)
    {
        // Same answer for "does not exist" and "belongs to someone else": never reveal other travellers' bookings.
        if (!BookingId.TryParse(bookingId, out _))
        {
            throw ProblemException.NotFound();
        }

        var query = Load(db.Bookings.Where(b => b.Id == bookingId && b.UserId == user.Id));
        if (!track)
        {
            query = query.AsNoTracking();
        }
        return await query.SingleOrDefaultAsync(ct) ?? throw ProblemException.NotFound();
    }

    private static List<FieldError> Validate(CreateBookingCommand command)
    {
        var errors = new List<FieldError>();
        if (!TripOptionId.TryParse(command.OptionId, out _))
        {
            errors.Add(new FieldError("optionId", "INVALID_ID"));
        }

        var passengers = command.Passengers;
        if (passengers.Count is < 1 or > 9)
        {
            errors.Add(new FieldError("passengers", "PASSENGER_COUNT", new Dictionary<string, object?> { ["min"] = 1, ["max"] = 9 }));
        }

        for (var i = 0; i < passengers.Count; i++)
        {
            var p = passengers[i];
            if (string.IsNullOrWhiteSpace(p.FullName) || p.FullName.Trim().Length > 120)
            {
                errors.Add(new FieldError($"passengers[{i}].fullName", "NAME_REQUIRED"));
            }
            if (!Enum.TryParse<PassengerKind>(p.PassengerType, ignoreCase: true, out _))
            {
                errors.Add(new FieldError($"passengers[{i}].passengerType", "INVALID_PASSENGER_TYPE"));
            }
            if (p.TravellerId is not null && !TravellerId.TryParse(p.TravellerId, out _))
            {
                errors.Add(new FieldError($"passengers[{i}].travellerId", "INVALID_ID"));
            }
            if (p.Phone is { Length: > 32 })
            {
                errors.Add(new FieldError($"passengers[{i}].phone", "PHONE_INVALID"));
            }
        }

        var adults = passengers.Count(p => string.Equals(p.PassengerType, "adult", StringComparison.OrdinalIgnoreCase));
        var infants = passengers.Count(p => string.Equals(p.PassengerType, "infant", StringComparison.OrdinalIgnoreCase));
        if (passengers.Count > 0 && adults == 0)
        {
            errors.Add(new FieldError("passengers", "ADULT_REQUIRED"));
        }
        if (infants > adults)
        {
            errors.Add(new FieldError("passengers", "TOO_MANY_INFANTS"));
        }
        return errors;
    }

    private static string Hash(CreateBookingCommand command)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            command.OptionId,
            Passengers = command.Passengers.Select(p => new { p.TravellerId, FullName = p.FullName.Trim(), PassengerType = p.PassengerType.ToLowerInvariant(), p.Phone }),
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    private static string EncodeCursor(Booking last) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes($"{last.CreatedAt.UtcTicks}|{last.Id}")).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static bool TryDecodeCursor(string cursor, out DateTimeOffset at, out string id)
    {
        at = default;
        id = "";
        try
        {
            var padded = cursor.Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
            var parts = Encoding.UTF8.GetString(Convert.FromBase64String(padded)).Split('|');
            if (parts.Length != 2 || !long.TryParse(parts[0], out var ticks) || !BookingId.TryParse(parts[1], out _))
            {
                return false;
            }
            at = new DateTimeOffset(ticks, TimeSpan.Zero);
            id = parts[1];
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

internal sealed class PaymentOutcomeUnknownException(Exception inner) : Exception("Payment outcome unknown.", inner);
