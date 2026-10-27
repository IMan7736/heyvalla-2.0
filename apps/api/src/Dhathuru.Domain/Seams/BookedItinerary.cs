using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Trips;

namespace Dhathuru.Domain.Seams;

/// <summary>
/// What the re-planner needs to know about a booking. Bookings implements it; the trip engine only sees this seam.
/// Implementations must scope the lookup to the signed-in owner (the planner endpoint is authenticated, not owner-aware).
/// </summary>
public interface IBookedItineraryProvider
{
    Task<BookedItinerary?> GetAsync(BookingId id, CancellationToken ct);
}

/// <summary>The confirmed legs of a booking in travel order, with the party and the preferences it was booked under.</summary>
public sealed record BookedItinerary(IReadOnlyList<Leg> Legs, Passengers Passengers, TripPreferences Preferences);
