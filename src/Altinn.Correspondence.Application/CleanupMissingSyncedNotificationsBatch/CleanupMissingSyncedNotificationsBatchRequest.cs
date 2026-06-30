namespace Altinn.Correspondence.Application.CleanupMissingSyncedNotificationsBatch;

public record CleanupMissingSyncedNotificationsBatchRequest
{
    /// <summary>
    /// Cursor timestamp for pagination (NotificationSent field, not Created)
    /// </summary>
    public DateTimeOffset? CursorNotificationSent { get; init; }

    /// <summary>
    /// Cursor Id for composite keyset pagination
    /// </summary>
    public Guid? CursorId { get; init; }

    /// <summary>
    /// Number of notifications to fetch per batch
    /// </summary>
    public int BatchSize { get; init; } = 1000;

    /// <summary>
    /// Total number of correspondences processed across all batches
    /// </summary>
    public int TotalCorrespondencesProcessed { get; init; }

    /// <summary>
    /// Total number of notifications processed across all batches
    /// </summary>
    public int TotalNotificationsProcessed { get; init; }
}
