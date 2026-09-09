using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Core.Models;

/// <summary>
/// Per-correspondence daily summary data DTO returned from repository queries.
/// This is a data transfer object used between the persistence and application layers.
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
    public int MessageCount { get; set; } = 1;
    public long DatabaseStorageBytes { get; set; }
    public long AttachmentStorageBytes { get; set; }
    public Guid? ShipmentId { get; set; }
    public Guid? ReminderShipmentId { get; set; }
}
