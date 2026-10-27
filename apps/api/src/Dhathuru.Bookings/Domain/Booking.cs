using Dhathuru.Domain.Pricing;

namespace Dhathuru.Bookings.Domain;

/// <summary>Aggregate root: one purchase of a TripOption.</summary>
public sealed class Booking
{
    private readonly List<BookingLeg> _legs = [];
    private readonly List<BookingPassenger> _passengers = [];
    private readonly List<PaymentRecord> _payments = [];
    private readonly List<Ticket> _tickets = [];
    private readonly List<AuditEntry> _audit = [];
    private readonly List<Disruption> _disruptions = [];

    private Booking()
    {
    }

    public string Id { get; private set; } = "";

    public string UserId { get; private set; } = "";

    public string OptionId { get; private set; } = "";

    public BookingStatus Status { get; private set; }

    public long TotalMinor { get; private set; }

    public string Currency { get; private set; } = Money.Mvr;

    public DateTimeOffset? HoldExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public string? FailureCode { get; private set; }

    public string? ContactEmail { get; private set; }

    public string Locale { get; private set; } = "en";

    /// <summary>Idempotency key of the create call; unique per user.</summary>
    public string CreateKey { get; private set; } = "";

    /// <summary>Hash of the create request body so a reused key with another body is rejected.</summary>
    public string CreateRequestHash { get; private set; } = "";

    /// <summary>JSON array of price lines (code, amountMinor, legIndex?). Fees are shown, never hidden.</summary>
    public string PriceLinesJson { get; private set; } = "[]";

    public uint Version { get; private set; }

    public IReadOnlyList<BookingLeg> Legs => _legs;

    public IReadOnlyList<BookingPassenger> Passengers => _passengers;

    public IReadOnlyList<PaymentRecord> Payments => _payments;

    public IReadOnlyList<Ticket> Tickets => _tickets;

    public IReadOnlyList<AuditEntry> Audit => _audit;

    public IReadOnlyList<Disruption> Disruptions => _disruptions;

    public Money Total => new(TotalMinor, Currency);

    /// <summary>The latest payment attempt (a declined card can be retried within the hold).</summary>
    public PaymentRecord? CurrentPayment => _payments.OrderByDescending(p => p.Attempt).FirstOrDefault();

    public static Booking Create(
        string id,
        string userId,
        string optionId,
        Money total,
        string priceLinesJson,
        string? contactEmail,
        string locale,
        string createKey,
        string createRequestHash,
        DateTimeOffset now)
    {
        var booking = new Booking
        {
            Id = id,
            UserId = userId,
            OptionId = optionId,
            Status = BookingStatus.Draft,
            TotalMinor = total.AmountMinor,
            Currency = total.Currency,
            PriceLinesJson = priceLinesJson,
            ContactEmail = contactEmail,
            Locale = locale,
            CreateKey = createKey,
            CreateRequestHash = createRequestHash,
            CreatedAt = now,
            UpdatedAt = now,
        };
        booking.Record("created", null, BookingStatus.Draft, now, "system");
        return booking;
    }

    public void AddLeg(BookingLeg leg) => _legs.Add(leg);

    public void AddPassenger(BookingPassenger passenger) => _passengers.Add(passenger);

    public void AddPayment(PaymentRecord payment) => _payments.Add(payment);

    public void AddTicket(Ticket ticket) => _tickets.Add(ticket);

    public void AddDisruption(Disruption disruption) => _disruptions.Add(disruption);

    public void Transition(BookingStatus to, DateTimeOffset now, string actor, string? detail = null, string? failureCode = null)
    {
        if (Status == to)
        {
            return;
        }

        if (!BookingStateMachine.CanTransition(Status, to))
        {
            throw new InvalidBookingTransitionException(Status, to);
        }

        var from = Status;
        Status = to;
        UpdatedAt = now;
        if (failureCode is not null)
        {
            FailureCode = failureCode;
        }

        if (!BookingStateMachine.HoldsSeats(to))
        {
            HoldExpiresAt = null;
        }

        Record(detail ?? "transition", from, to, now, actor);
    }

    public void StartHold(DateTimeOffset expiresAt, DateTimeOffset now, string actor)
    {
        HoldExpiresAt = expiresAt;
        Transition(BookingStatus.Held, now, actor, "held");
    }

    public void ReplaceTotal(Money total, string priceLinesJson)
    {
        TotalMinor = total.AmountMinor;
        PriceLinesJson = priceLinesJson;
    }

    public bool IsHoldExpired(DateTimeOffset now) => HoldExpiresAt is { } at && at <= now;

    public void Note(string action, DateTimeOffset now, string actor, string? detail = null)
    {
        UpdatedAt = now;
        Record(action, Status, Status, now, actor, detail);
    }

    private void Record(string action, BookingStatus? from, BookingStatus to, DateTimeOffset now, string actor, string? detail = null) =>
        _audit.Add(new AuditEntry(Guid.NewGuid(), Id, now, action, from, to, actor, detail));
}

public sealed class BookingPassenger(string id, string bookingId, string fullName, PassengerKind kind, string? travellerId, string? phone)
{
    public string Id { get; private set; } = id;

    public string BookingId { get; private set; } = bookingId;

    public string FullName { get; private set; } = fullName;

    public PassengerKind Kind { get; private set; } = kind;

    public string? TravellerId { get; private set; } = travellerId;

    public string? Phone { get; private set; } = phone;
}

public sealed class BookingLeg
{
    private BookingLeg()
    {
    }

    public BookingLeg(string id, string bookingId, int legIndex, string legJson, string providerId, string? serviceRunId, bool bookable, long amountMinor)
    {
        Id = id;
        BookingId = bookingId;
        LegIndex = legIndex;
        LegJson = legJson;
        ProviderId = providerId;
        ServiceRunId = serviceRunId;
        Bookable = bookable;
        AmountMinor = amountMinor;
        Status = bookable ? BookingLegStatus.Pending : BookingLegStatus.Confirmed;
    }

    public string Id { get; private set; } = "";

    public string BookingId { get; private set; } = "";

    public int LegIndex { get; private set; }

    public BookingLegStatus Status { get; private set; }

    /// <summary>Snapshot of the trip-engine leg as it was booked (JSON). Never re-read from the planner.</summary>
    public string LegJson { get; private set; } = "{}";

    public string ProviderId { get; private set; } = "";

    public string? ServiceRunId { get; private set; }

    public bool Bookable { get; private set; }

    /// <summary>Total for all paying passengers on this leg, minor units.</summary>
    public long AmountMinor { get; private set; }

    public string? HoldReference { get; private set; }

    public string? ProviderReference { get; private set; }

    public string? FailureCode { get; private set; }

    public long RefundedMinor { get; private set; }

    /// <summary>True once the provider confirmed our hold or booking is gone. A failed leg can still hold a seat until this is set.</summary>
    public bool HoldReleased { get; private set; }

    /// <summary>The provider still owes us a seat release or cancellation for this leg.</summary>
    public bool NeedsRelease => Bookable && HoldReference is not null && !HoldReleased
        && Status is BookingLegStatus.Held or BookingLegStatus.Confirmed or BookingLegStatus.Failed;

    public void MarkHeld(string holdReference)
    {
        HoldReference = holdReference;
        Status = BookingLegStatus.Held;
    }

    public void MarkConfirmed(string providerReference)
    {
        ProviderReference = providerReference;
        Status = BookingLegStatus.Confirmed;
        FailureCode = null;
    }

    public void MarkFailed(string failureCode)
    {
        FailureCode = failureCode;
        Status = BookingLegStatus.Failed;
    }

    public void MarkReleased()
    {
        HoldReleased = true;
        if (Status is BookingLegStatus.Held or BookingLegStatus.Confirmed)
        {
            Status = BookingLegStatus.Released;
        }

        if (FailureCode == ReleasePending)
        {
            FailureCode = null;
        }
    }

    public const string ReleasePending = "RELEASE_PENDING";

    public void MarkReleasePending()
    {
        if (FailureCode is null)
        {
            FailureCode = ReleasePending;
        }
    }

    /// <summary>The provider refused or failed to cancel: the leg stays confirmed and the reason is kept.</summary>
    public void MarkFailedCancel(string failureCode) => FailureCode = failureCode;

    public void MarkCancelled(long refundMinor)
    {
        Status = BookingLegStatus.Cancelled;
        RefundedMinor = refundMinor;
        FailureCode = null;
    }
}

public sealed class PaymentRecord
{
    private PaymentRecord()
    {
    }

    public PaymentRecord(string id, string bookingId, int attempt, long amountMinor, string currency, string gateway, string idempotencyKey, DateTimeOffset createdAt)
    {
        Id = id;
        Attempt = attempt;
        BookingId = bookingId;
        AmountMinor = amountMinor;
        Currency = currency;
        Gateway = gateway;
        IdempotencyKey = idempotencyKey;
        Status = PaymentStatus.Pending;
        CreatedAt = createdAt;
    }

    public string Id { get; private set; } = "";

    public string BookingId { get; private set; } = "";

    /// <summary>1 for the first payment attempt on a booking, 2 for the retry after a decline, and so on.</summary>
    public int Attempt { get; private set; }

    public long AmountMinor { get; private set; }

    public string Currency { get; private set; } = Money.Mvr;

    public PaymentStatus Status { get; private set; }

    public string Gateway { get; private set; } = "";

    public string? GatewayReference { get; private set; }

    public string? FailureCode { get; private set; }

    public string IdempotencyKey { get; private set; } = "";

    /// <summary>Set while the issuer wants an extra verification step (fake 3-D Secure).</summary>
    public string? ChallengeId { get; private set; }

    public long RefundedMinor { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public void RequireAction(string challengeId, string gatewayReference)
    {
        Status = PaymentStatus.RequiresAction;
        ChallengeId = challengeId;
        GatewayReference = gatewayReference;
    }

    public void Succeed(string gatewayReference)
    {
        Status = PaymentStatus.Succeeded;
        GatewayReference = gatewayReference;
        ChallengeId = null;
        FailureCode = null;
    }

    public void Fail(string failureCode)
    {
        Status = PaymentStatus.Failed;
        FailureCode = failureCode;
        ChallengeId = null;
    }

    public void AddRefund(long amountMinor)
    {
        RefundedMinor = checked(RefundedMinor + amountMinor);
        Status = RefundedMinor >= AmountMinor ? PaymentStatus.Refunded : PaymentStatus.PartiallyRefunded;
    }
}

public sealed class RefundRecord(string id, string bookingId, string paymentId, long amountMinor, string currency, string reasonCode, string idempotencyKey, DateTimeOffset createdAt)
{
    public string Id { get; private set; } = id;

    public string BookingId { get; private set; } = bookingId;

    public string PaymentId { get; private set; } = paymentId;

    public long AmountMinor { get; private set; } = amountMinor;

    public string Currency { get; private set; } = currency;

    public string ReasonCode { get; private set; } = reasonCode;

    public string IdempotencyKey { get; private set; } = idempotencyKey;

    public RefundStatus Status { get; private set; } = RefundStatus.Pending;

    public string? FailureCode { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; } = createdAt;

    public void Succeed() => Status = RefundStatus.Succeeded;

    public void Fail(string failureCode)
    {
        Status = RefundStatus.Failed;
        FailureCode = failureCode;
    }
}

public sealed class Ticket(string id, string bookingId, string bookingLegId, string passengerId, string nonce, string keyId, DateTimeOffset issuedAt)
{
    public string Id { get; private set; } = id;

    public string BookingId { get; private set; } = bookingId;

    public string BookingLegId { get; private set; } = bookingLegId;

    public string PassengerId { get; private set; } = passengerId;

    public TicketStatus Status { get; private set; } = TicketStatus.Valid;

    public string Nonce { get; private set; } = nonce;

    public string KeyId { get; private set; } = keyId;

    public DateTimeOffset IssuedAt { get; private set; } = issuedAt;

    public DateTimeOffset? UsedAt { get; private set; }

    public void Cancel()
    {
        if (Status == TicketStatus.Valid)
        {
            Status = TicketStatus.Cancelled;
        }
    }

    public bool Use(DateTimeOffset now)
    {
        if (Status != TicketStatus.Valid)
        {
            return false;
        }
        Status = TicketStatus.Used;
        UsedAt = now;
        return true;
    }

    public void Expire()
    {
        if (Status == TicketStatus.Valid)
        {
            Status = TicketStatus.Expired;
        }
    }
}

public sealed record AuditEntry(Guid Id, string BookingId, DateTimeOffset At, string Action, BookingStatus? FromStatus, BookingStatus ToStatus, string Actor, string? Detail = null);

public enum DisruptionState { Open, Acknowledged, Resolved }

public sealed class Disruption
{
    private Disruption()
    {
    }

    public Disruption(string id, string bookingId, string bookingLegId, int legIndex, bool cancelled, int delayMinutes, DateTimeOffset now)
    {
        Id = id;
        BookingId = bookingId;
        BookingLegId = bookingLegId;
        LegIndex = legIndex;
        Cancelled = cancelled;
        DelayMinutes = delayMinutes;
        DetectedAt = now;
    }

    public string Id { get; private set; } = "";

    public string BookingId { get; private set; } = "";

    public string BookingLegId { get; private set; } = "";

    public int LegIndex { get; private set; }

    public bool Cancelled { get; private set; }

    public int DelayMinutes { get; private set; }

    public DisruptionState State { get; private set; } = DisruptionState.Open;

    public DateTimeOffset DetectedAt { get; private set; }

    public DateTimeOffset? NotifiedAt { get; private set; }

    /// <summary>JSON of the replan suggestion (option summaries) requested from the trip planner; null until available.</summary>
    public string? SuggestionJson { get; private set; }

    public void Update(bool cancelled, int delayMinutes)
    {
        Cancelled = cancelled;
        DelayMinutes = delayMinutes;
    }

    public void MarkNotified(DateTimeOffset now) => NotifiedAt = now;

    public void Acknowledge()
    {
        if (State == DisruptionState.Open)
        {
            State = DisruptionState.Acknowledged;
        }
    }

    public void SetSuggestion(string json) => SuggestionJson = json;

    public void Resolve() => State = DisruptionState.Resolved;
}
