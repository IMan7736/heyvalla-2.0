using Dhathuru.Domain.Time;

namespace Dhathuru.Domain.Tests;

public class MaldivesTimeTests
{
    [Fact]
    public void Local_five_pm_is_noon_utc()
    {
        var utc = MaldivesTime.FromLocal(new DateOnly(2026, 10, 27), new TimeOnly(17, 0));
        Assert.Equal(new DateTimeOffset(2026, 10, 27, 12, 0, 0, TimeSpan.Zero), utc);
    }

    [Fact]
    public void Day_starts_at_19_00_utc_the_evening_before()
    {
        var (start, end) = MaldivesTime.DayBounds(new DateOnly(2026, 10, 27));
        Assert.Equal(new DateTimeOffset(2026, 10, 26, 19, 0, 0, TimeSpan.Zero), start);
        Assert.Equal(TimeSpan.FromDays(1), end - start);
    }

    [Fact]
    public void Local_date_crosses_midnight_before_utc_does()
    {
        var instant = new DateTimeOffset(2026, 10, 26, 20, 0, 0, TimeSpan.Zero);
        Assert.Equal(new DateOnly(2026, 10, 27), MaldivesTime.LocalDate(instant));
        Assert.Equal(TimeSpan.FromHours(5), MaldivesTime.ToLocal(instant).Offset);
    }
}
