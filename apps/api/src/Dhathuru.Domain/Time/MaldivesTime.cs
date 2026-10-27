namespace Dhathuru.Domain.Time;

/// <summary>
/// Everything is stored and transported in UTC. The Maldives (Indian/Maldives) is UTC+5 all year with no DST,
/// so display conversion uses a fixed offset and needs no time zone database.
/// </summary>
public static class MaldivesTime
{
    public const string ZoneId = "Indian/Maldives";

    public static readonly TimeSpan Offset = TimeSpan.FromHours(5);

    public static DateTimeOffset ToLocal(DateTimeOffset instant) => instant.ToOffset(Offset);

    public static DateOnly LocalDate(DateTimeOffset instant) => DateOnly.FromDateTime(ToLocal(instant).DateTime);

    /// <summary>Combine a local Maldives date and time of day into a UTC instant.</summary>
    public static DateTimeOffset FromLocal(DateOnly date, TimeOnly time) =>
        new DateTimeOffset(date.ToDateTime(time), Offset).ToUniversalTime();

    /// <summary>Start (inclusive) and end (exclusive) of a local Maldives day, as UTC instants.</summary>
    public static (DateTimeOffset Start, DateTimeOffset End) DayBounds(DateOnly date)
    {
        var start = FromLocal(date, TimeOnly.MinValue);
        return (start, start.AddDays(1));
    }
}
