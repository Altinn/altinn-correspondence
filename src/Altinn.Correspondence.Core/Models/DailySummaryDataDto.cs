using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Core.Models;

/// <summary>
/// Daily summary data DTO returned from repository queries.
/// One row per correspondence notification (or one row with null notification fields when none exist).
/// </summary>
public class DailySummaryDataDto
{
    public Guid CorrespondenceId { get; set; }
    public DateTime Date { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public int Day { get; set; }
    public string ServiceOwnerId { get; set; } = string.Empty;
    public string? ServiceOwnerName { get; set; }
    public string MessageSender { get; set; } = string.Empty;
    public string? SenderOrgNumber { get; set; }
    public string ResourceId { get; set; } = string.Empty;
    public RecipientType RecipientType { get; set; }
    public AltinnVersion AltinnVersion { get; set; }
    public long DatabaseStorageBytes { get; set; }
    public long AttachmentStorageBytes { get; set; }
    public Guid? ShipmentId { get; set; }
    public bool? IsReminder { get; set; }
    public DateTimeOffset? NotificationSent { get; set; }
}
