namespace Dhathuru.Bookings.Domain;

/// <summary>
/// The single place that decides which booking transitions are legal.
/// Draft -> Held -> PaymentPending -> Confirmed | Failed | Expired;
/// Confirmed -> PartiallyCancelled | Cancelled; PartiallyCancelled -> Cancelled;
/// Cancelled -> Refunded once money has fully returned. Failed and Expired are terminal.
/// </summary>
public static class BookingStateMachine
{
    private static readonly IReadOnlyDictionary<BookingStatus, BookingStatus[]> Allowed =
        new Dictionary<BookingStatus, BookingStatus[]>
        {
            [BookingStatus.Draft] = [BookingStatus.Held, BookingStatus.Failed],
            [BookingStatus.Held] = [BookingStatus.PaymentPending, BookingStatus.Expired, BookingStatus.Cancelled, BookingStatus.Failed],
            [BookingStatus.PaymentPending] = [BookingStatus.Confirmed, BookingStatus.Failed, BookingStatus.Expired, BookingStatus.Held],
            [BookingStatus.Confirmed] = [BookingStatus.PartiallyCancelled, BookingStatus.Cancelled],
            [BookingStatus.PartiallyCancelled] = [BookingStatus.PartiallyCancelled, BookingStatus.Cancelled],
            [BookingStatus.Cancelled] = [BookingStatus.Refunded],
            [BookingStatus.Failed] = [],
            [BookingStatus.Expired] = [],
            [BookingStatus.Refunded] = [],
        };

    public static bool CanTransition(BookingStatus from, BookingStatus to) => Allowed[from].Contains(to);

    public static bool IsTerminal(BookingStatus status) => Allowed[status].Length == 0;

    /// <summary>States in which provider seats are (or may be) held and must be released if we stop.</summary>
    public static bool HoldsSeats(BookingStatus status) => status is BookingStatus.Held or BookingStatus.PaymentPending;
}

public sealed class InvalidBookingTransitionException(BookingStatus from, BookingStatus to)
    : InvalidOperationException($"Booking cannot move from {from} to {to}.")
{
    public BookingStatus From { get; } = from;

    public BookingStatus To { get; } = to;
}
