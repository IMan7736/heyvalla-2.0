using Dhathuru.Bookings.Domain;
using Dhathuru.Bookings.Persistence;
using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Security;
using Dhathuru.Domain.Seams;
using Dhathuru.Domain.Trips;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Dhathuru.Bookings.Services;

/// <summary>
/// Gives the trip engine the booked legs for <c>POST /trips/replan</c>. The planner is a singleton, so this resolves the
/// signed-in user from the current request and opens its own scope for the database. A booking that is not the caller's
/// is reported as not found, like everywhere else (IDOR rule).
/// </summary>
internal sealed class BookedItineraryProvider(IServiceScopeFactory scopes, IHttpContextAccessor accessor) : IBookedItineraryProvider
{
    // Bookings do not store the search preferences; a replan is always judged with the balanced default.
    private static readonly TripPreferences DefaultPreferences = new(RiskTolerance.Balanced);

    public async Task<BookedItinerary?> GetAsync(BookingId id, CancellationToken ct)
    {
        var services = accessor.HttpContext?.RequestServices;
        var user = services?.GetService<ICurrentUser>();
        if (user is null || !user.IsAuthenticated)
        {
            return null;
        }

        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<BookingsDbContext>();
        var booking = await db.Bookings.AsNoTracking()
            .Include(b => b.Legs).Include(b => b.Passengers)
            .AsSplitQuery()
            .FirstOrDefaultAsync(b => b.Id == id.Value && b.UserId == user.UserId, ct);
        if (booking is null || booking.Status is not (BookingStatus.Confirmed or BookingStatus.PartiallyCancelled))
        {
            return null;
        }

        var legs = booking.Legs
            .Where(l => l.Status is BookingLegStatus.Confirmed)
            .OrderBy(l => l.LegIndex)
            .Select(l => LegSnapshots.FromJson(System.Text.Json.Nodes.JsonNode.Parse(l.LegJson)!.AsObject(), l.ServiceRunId))
            .ToList();
        if (legs.Count == 0)
        {
            return null;
        }

        var passengers = new Passengers(
            Math.Max(1, booking.Passengers.Count(p => p.Kind == PassengerKind.Adult)),
            booking.Passengers.Count(p => p.Kind == PassengerKind.Child),
            booking.Passengers.Count(p => p.Kind == PassengerKind.Infant));
        return new BookedItinerary(legs, passengers, DefaultPreferences);
    }
}
