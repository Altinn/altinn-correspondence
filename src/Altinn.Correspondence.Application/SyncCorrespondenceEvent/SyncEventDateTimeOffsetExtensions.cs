namespace Altinn.Correspondence.Application.SyncCorrespondenceEvent;

/// <summary>
/// Extensions for DateTimeOffset to compare equality within a second, useful for evaluating duplicate sync events.
/// </summary>
public static class SyncEventDateTimeOffsetExtensions
{
    /// <summary>
    /// Compares two DateTimeOffset instances for equality within a second, which is useful for determining if two sync events are duplicates.
    /// </summary>
    /// <param name="dto1"></param>
    /// <param name="dto2"></param>
    /// <returns></returns>
    public static bool EqualsToSecond(this DateTimeOffset dto1, DateTimeOffset dto2)
    {
        // Normalize to UTC to handle different offsets correctly
        DateTimeOffset utcDto1 = dto1.ToUniversalTime();
        DateTimeOffset utcDto2 = dto2.ToUniversalTime();

        // Truncate to the second by creating a new DateTimeOffset
        // with milliseconds, microseconds, and ticks set to zero.
        DateTimeOffset truncatedDto1 = new DateTimeOffset(
            utcDto1.Year, utcDto1.Month, utcDto1.Day,
            utcDto1.Hour, utcDto1.Minute, utcDto1.Second,
            TimeSpan.Zero // Set offset to zero for UTC
        );

        DateTimeOffset truncatedDto2 = new DateTimeOffset(
            utcDto2.Year, utcDto2.Month, utcDto2.Day,
            utcDto2.Hour, utcDto2.Minute, utcDto2.Second,
            TimeSpan.Zero // Set offset to zero for UTC
        );

        return truncatedDto1.Equals(truncatedDto2);
    }
}