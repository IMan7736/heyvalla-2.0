using Dhathuru.Bookings.Domain;
using Dhathuru.Bookings.Payments;

namespace Dhathuru.Bookings.Services;

/// <summary>Seam that lets the fake <c>tok_fake_leg_failure</c> token fail a leg during development. Never active in production.</summary>
public interface IConfirmFaultInjector
{
    bool ShouldFailConfirm(Booking booking, BookingLeg leg, string? paymentToken);
}

public sealed class NoFaultInjector : IConfirmFaultInjector
{
    public bool ShouldFailConfirm(Booking booking, BookingLeg leg, string? paymentToken) => false;
}

/// <summary>Fails the last bookable leg's confirmation when the fake leg-failure token is used.</summary>
public sealed class FakeTokenFaultInjector : IConfirmFaultInjector
{
    public bool ShouldFailConfirm(Booking booking, BookingLeg leg, string? paymentToken) =>
        paymentToken == FakeTokens.LegFailure
        && leg.Bookable
        && leg.LegIndex == booking.Legs.Where(l => l.Bookable).Max(l => l.LegIndex);
}
