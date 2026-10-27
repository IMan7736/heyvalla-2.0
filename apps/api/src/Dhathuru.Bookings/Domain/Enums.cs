namespace Dhathuru.Bookings.Domain;

/// <summary>Booking lifecycle from docs/CONTRACT.md. Wire values are snake_case (see <see cref="WireNames"/>).</summary>
public enum BookingStatus
{
    Draft,
    Held,
    PaymentPending,
    Confirmed,
    Failed,
    Expired,
    PartiallyCancelled,
    Cancelled,
    Refunded,
}

public enum BookingLegStatus { Pending, Held, Confirmed, Failed, Cancelled, Released }

public enum PaymentStatus { Pending, RequiresAction, Succeeded, Failed, Refunded, PartiallyRefunded }

public enum TicketStatus { Valid, Used, Cancelled, Expired }

public enum PassengerKind { Adult, Child, Infant }

public enum RefundStatus { Pending, Succeeded, Failed }

public static class WireNames
{
    public static string Of(BookingStatus s) => s switch
    {
        BookingStatus.Draft => "draft",
        BookingStatus.Held => "held",
        BookingStatus.PaymentPending => "payment_pending",
        BookingStatus.Confirmed => "confirmed",
        BookingStatus.Failed => "failed",
        BookingStatus.Expired => "expired",
        BookingStatus.PartiallyCancelled => "partially_cancelled",
        BookingStatus.Cancelled => "cancelled",
        BookingStatus.Refunded => "refunded",
        _ => throw new ArgumentOutOfRangeException(nameof(s)),
    };

    public static bool TryParse(string? value, out BookingStatus status)
    {
        foreach (var candidate in Enum.GetValues<BookingStatus>())
        {
            if (string.Equals(Of(candidate), value, StringComparison.Ordinal))
            {
                status = candidate;
                return true;
            }
        }
        status = default;
        return false;
    }

    public static string Of(BookingLegStatus s) => s.ToString().ToLowerInvariant();

    public static string Of(TicketStatus s) => s.ToString().ToLowerInvariant();

    public static string Of(PassengerKind k) => k.ToString().ToLowerInvariant();

    public static string Of(PaymentStatus s) => s switch
    {
        PaymentStatus.RequiresAction => "requires_action",
        PaymentStatus.PartiallyRefunded => "partially_refunded",
        _ => s.ToString().ToLowerInvariant(),
    };
}
