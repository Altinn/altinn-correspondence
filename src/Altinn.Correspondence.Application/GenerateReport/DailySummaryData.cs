using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Application.GenerateReport;

/// <summary>
/// Per-notification data for cost allocation and reporting.
/// Each row represents one correspondence notification (or a correspondence with no notifications).
/// </summary>
public class DailySummaryData
{
    /// <summary>
    /// Correspondence ID
    /// </summary>
    public Guid CorrespondenceId { get; set; }

    /// <summary>
    /// Date in YYYY-MM-DD format
    /// </summary>
    public DateTime Date { get; set; }
    
    /// <summary>
    /// Year (YYYY)
    /// </summary>
    public int Year { get; set; }
    
    /// <summary>
    /// Month (MM)
    /// </summary>
    public int Month { get; set; }
    
    /// <summary>
    /// Day (DD)
    /// </summary>
    public int Day { get; set; }
    
    /// <summary>
    /// Service Owner ID (organization number)
    /// </summary>
    public string ServiceOwnerId { get; set; } = string.Empty;
    
    /// <summary>
    /// Service Owner Name (for readability)
    /// </summary>
    public string ServiceOwnerName { get; set; } = string.Empty;
    
    /// <summary>
    /// Message sender
    /// </summary>
    public string MessageSender { get; set; } = string.Empty;

    /// <summary>
    /// Sender organization number (if available in propertyList)
    /// </summary>
    public string SenderOrgNumber { get; set; } = string.Empty;

    /// <summary>
    /// Resource ID
    /// </summary>
    public string ResourceId { get; set; } = string.Empty;
    
    /// <summary>
    /// Resource title in Norwegian (from Resource Registry)
    /// </summary>
    public string ResourceTitle { get; set; } = string.Empty;
    
    /// <summary>
    /// Recipient type (Organization or Person)
    /// </summary>
    public RecipientType RecipientType { get; set; }
    
    /// <summary>
    /// Altinn version (Altinn2 or Altinn3)
    /// </summary>
    public AltinnVersion AltinnVersion { get; set; }
    
    /// <summary>
    /// Total database storage used (metadata) in bytes
    /// </summary>
    public long DatabaseStorageBytes { get; set; }
    
    /// <summary>
    /// Total attachment storage used in bytes
    /// </summary>
    public long AttachmentStorageBytes { get; set; }

    /// <summary>
    /// Notification shipment ID, if any
    /// </summary>
    public Guid? ShipmentId { get; set; }

    /// <summary>
    /// Whether this row is a reminder notification. Null when the correspondence has no notifications.
    /// </summary>
    public bool? IsReminder { get; set; }
}
