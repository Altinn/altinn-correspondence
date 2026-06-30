namespace Altinn.Correspondence.Core.Models.Notifications;

/// <summary>
/// Represents a correspondence grouped with its synced notification events
/// </summary>
public class CorrespondenceWithNotifications
{
    public Guid CorrespondenceId { get; set; }
    public List<Guid> NotificationIds { get; set; } = new();
}

/// <summary>
/// Batch result containing correspondences with notifications and cursor information for pagination
/// </summary>
public class CorrespondencesWithNotificationsBatch
{
    public List<CorrespondenceWithNotifications> Correspondences { get; set; } = new();
    public DateTimeOffset? OldestNotificationTimestamp { get; set; }
    public Guid? OldestNotificationId { get; set; }
    public int TotalNotificationCount { get; set; }
}
