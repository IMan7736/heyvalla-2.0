using Dhathuru.Domain.Ids;
using Dhathuru.Domain.Places;
using Dhathuru.Domain.Pricing;
using Dhathuru.Domain.Seams;
using Dhathuru.Domain.Time;
using Dhathuru.Domain.Transport;
using Dhathuru.Domain.Trips;
using Dhathuru.TripEngine.Configuration;
using Dhathuru.TripEngine.Fixtures;

namespace Dhathuru.TripEngine.Tests;

public sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

public sealed class FakeBookings(Dictionary<BookingId, BookedItinerary> items) : IBookedItineraryProvider
{
    public Task<BookedItinerary?> GetAsync(BookingId id, CancellationToken ct) =>
        Task.FromResult(items.TryGetValue(id, out var b) ? b : null);
}

internal static class H
{
    /// <summary>The fake "now": the day before every scenario, 12:00 Maldives time.</summary>
    public static readonly DateTimeOffset Now = new(2026, 10, 26, 7, 0, 0, TimeSpan.Zero);

    public static TripPlanner Planner(
        WorldBuilder world,
        Dictionary<BookingId, BookedItinerary>? bookings = null,
        IDictionary<ServiceId, ReliabilityStat>? stats = null,
        EngineOptions? options = null) =>
        new(new StaticSnapshotProvider(world.Build()), new StaticReliability(stats),
            bookings is null ? null : new FakeBookings(bookings), options, null, new FixedClock(Now));

    public static TripSearchQuery Q(
        PlaceId from,
        PlaceId to,
        string date,
        string time,
        TimeMode mode = TimeMode.DepartAfter,
        RiskTolerance tolerance = RiskTolerance.Balanced,
        int adults = 1,
        int? maxTransfers = null,
        Money? budget = null,
        IReadOnlyList<TransportMode>? modes = null) =>
        new(from, to, DateOnly.Parse(date), TimeOnly.Parse(time), mode, new Passengers(adults),
            new TripPreferences(tolerance, maxTransfers, budget, modes));

    public static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static string Hhmm(DateTimeOffset t) => MaldivesTime.ToLocal(t).ToString("HH:mm");

    public static DateTimeOffset At(string localDateTime) => WorldBuilder.Local(localDateTime);

    public static bool Uses(TripOption o, string hhmm, TransportMode mode) =>
        o.Legs.Any(l => l.Mode == mode && Hhmm(l.DepartAt) == hhmm);

    /// <summary>Independent structural invariants every returned option must satisfy.</summary>
    public static void AssertSound(TripOption o)
    {
        Assert.NotEmpty(o.Legs);
        Assert.Equal(o.Legs.Count - 1, o.Transfers);
        Assert.Equal(o.Legs[0].DepartAt, o.DepartAt);
        Assert.Equal(o.Legs[^1].ArriveAt, o.ArriveAt);
        for (var i = 0; i < o.Legs.Count; i++)
        {
            Assert.True(o.Legs[i].ArriveAt > o.Legs[i].DepartAt, $"leg {i} has non-positive duration");
            if (i == 0) continue;
            Assert.Equal(o.Legs[i - 1].To.Id, o.Legs[i].From.Id);
            Assert.True(o.Legs[i].DepartAt >= o.Legs[i - 1].ArriveAt, $"leg {i} departs before leg {i - 1} arrives");
        }
        Assert.Equal(o.Legs.Count - 1, o.Buffers.Count);
        foreach (var b in o.Buffers)
        {
            var gap = (int)(o.Legs[b.AfterLegIndex + 1].DepartAt - o.Legs[b.AfterLegIndex].ArriveAt).TotalMinutes;
            Assert.Equal(gap, b.Minutes);
            Assert.True(b.Minutes >= b.RequiredMinutes, $"buffer after leg {b.AfterLegIndex}: {b.Minutes} < required {b.RequiredMinutes}");
            Assert.True(b.ComfortMinutes >= b.RequiredMinutes);
        }
        Assert.Equal(o.Legs.Sum(l => l.Price.AmountMinor), o.Price.Total.AmountMinor);
        Assert.InRange(o.Risk.MissProbability, 0, 1);
        Assert.InRange(o.Score, 0, 100);
        Assert.All(o.Explanations, e => Assert.False(string.IsNullOrWhiteSpace(e.Code)));
    }
}
