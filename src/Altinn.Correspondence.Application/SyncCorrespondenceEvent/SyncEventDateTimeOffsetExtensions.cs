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
        return dto1.TruncateToSecondUtc() == dto2.TruncateToSecondUtc();
    }

    public static DateTimeOffset TruncateToSecondUtc(this DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, utc.Second, TimeSpan.Zero);
    }
}