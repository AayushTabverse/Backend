namespace menu_backend.Helpers;

/// <summary>
/// Converts between stored UTC timestamps and the restaurant's local calendar.
///
/// All timestamps are stored in UTC (DateTime.UtcNow). Anything that means a *calendar* day or
/// hour — "today's sales", bill numbers, date-range filters, peak hours — must use local days,
/// otherwise orders between midnight and 05:30 IST land on the previous day.
/// The timezone comes from config "App:TimeZone" (default Asia/Kolkata).
/// </summary>
public static class BusinessClock
{
    private static TimeZoneInfo _zone = FindZone("Asia/Kolkata");

    public static void Configure(string? timeZoneId)
    {
        if (!string.IsNullOrWhiteSpace(timeZoneId))
            _zone = FindZone(timeZoneId);
    }

    public static TimeZoneInfo Zone => _zone;

    /// <summary>Current local date-time in the restaurant's timezone.</summary>
    public static DateTime Now => ToLocal(DateTime.UtcNow);

    /// <summary>Today's calendar date in the restaurant's timezone.</summary>
    public static DateOnly Today => DateOnly.FromDateTime(Now);

    /// <summary>Converts a stored UTC timestamp to restaurant-local time.</summary>
    public static DateTime ToLocal(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _zone);

    /// <summary>The UTC instant at which the given local calendar day starts.</summary>
    public static DateTime StartOfDayUtc(DateOnly day) =>
        TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified), _zone);

    /// <summary>
    /// UTC range [start, end) covering the local calendar days from..to inclusive.
    /// Only the date part of the inputs is used.
    /// </summary>
    public static (DateTime StartUtc, DateTime EndUtc) DayRangeUtc(DateTime from, DateTime to) =>
        (StartOfDayUtc(DateOnly.FromDateTime(from)), StartOfDayUtc(DateOnly.FromDateTime(to).AddDays(1)));

    /// <summary>UTC range [start, end) covering a single local calendar day.</summary>
    public static (DateTime StartUtc, DateTime EndUtc) DayRangeUtc(DateOnly day) =>
        (StartOfDayUtc(day), StartOfDayUtc(day.AddDays(1)));

    private static TimeZoneInfo FindZone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException)
        {
            // Windows hosts use Windows zone IDs
            return id == "Asia/Kolkata"
                ? TimeZoneInfo.FindSystemTimeZoneById("India Standard Time")
                : throw new InvalidOperationException($"Unknown time zone '{id}' in App:TimeZone.");
        }
    }
}
