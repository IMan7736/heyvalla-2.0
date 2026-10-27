using Dhathuru.Bookings.Domain;
using Dhathuru.Bookings.Services;
using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Security;
using Dhathuru.Domain.Trips;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Dhathuru.Bookings.Tests;

[Collection("postgres")]
public class BookedItineraryTests(PostgresFixture pg)
{
    private sealed class FakeUser(string? id) : ICurrentUser
    {
        public bool IsAuthenticated => id is not null;

        public string UserId => id ?? throw new InvalidOperationException();

        public string? SessionId => null;

        public bool EmailVerified => true;

        public bool IsInRole(string role) => false;
    }

    private static BookedItineraryProvider ProviderFor(Harness h, string? userId)
    {
        var requestServices = new ServiceCollection().AddSingleton<ICurrentUser>(new FakeUser(userId)).BuildServiceProvider();
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext { RequestServices = requestServices } };
        return new BookedItineraryProvider(h.Root.GetRequiredService<IServiceScopeFactory>(), accessor);
    }

    private async Task<(Harness H, CurrentUser User, string BookingId, TripOption Option)> ConfirmedAsync()
    {
        var h = await Harness.CreateAsync(pg);
        var option = h.AddTwoLegOption();
        var user = Harness.User();
        await using var scope = h.Scope();
        var s = scope.ServiceProvider.GetRequiredService<BookingService>();
        var booking = await s.CreateAsync(user, Harness.Key(), Harness.Command(option), TestContext.Current.CancellationToken);
        await s.PayAsync(user, booking.Id, Harness.Key(), new PayCommand("card", Payments.FakeTokens.Success), TestContext.Current.CancellationToken);
        return (h, user, booking.Id, option);
    }

    [Fact]
    public async Task Owner_gets_the_confirmed_legs_exactly_as_they_were_booked()
    {
        var (h, user, bookingId, option) = await ConfirmedAsync();
        await using var _ = h;

        var itinerary = await ProviderFor(h, user.Id).GetAsync(BookingId.Parse(bookingId), TestContext.Current.CancellationToken);

        Assert.NotNull(itinerary);
        Assert.Equal(option.Legs.Select(l => l.Id), itinerary.Legs.Select(l => l.Id));
        Assert.Equal(option.Legs.Select(l => l.DepartAt.ToUniversalTime()), itinerary.Legs.Select(l => l.DepartAt.ToUniversalTime()));
        Assert.Equal(option.Legs.Select(l => l.ServiceRunId), itinerary.Legs.Select(l => l.ServiceRunId));
        Assert.Equal(option.Legs.Select(l => l.Price), itinerary.Legs.Select(l => l.Price));
        Assert.Equal(option.Legs.Select(l => l.From.Id), itinerary.Legs.Select(l => l.From.Id));
        Assert.Equal(1, itinerary.Passengers.Adults);
    }

    [Fact]
    public async Task Someone_else_or_nobody_gets_nothing()
    {
        var (h, _, bookingId, _) = await ConfirmedAsync();
        await using var _ = h;

        Assert.Null(await ProviderFor(h, Harness.User().Id).GetAsync(BookingId.Parse(bookingId), TestContext.Current.CancellationToken));
        Assert.Null(await ProviderFor(h, null).GetAsync(BookingId.Parse(bookingId), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task A_booking_that_is_not_confirmed_is_not_replannable()
    {
        await using var h = await Harness.CreateAsync(pg);
        var option = h.AddTwoLegOption();
        var user = Harness.User();
        await using var scope = h.Scope();
        var s = scope.ServiceProvider.GetRequiredService<BookingService>();
        var booking = await s.CreateAsync(user, Harness.Key(), Harness.Command(option), TestContext.Current.CancellationToken);

        Assert.Null(await ProviderFor(h, user.Id).GetAsync(BookingId.Parse(booking.Id), TestContext.Current.CancellationToken));
    }
}
