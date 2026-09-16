using System.Text.Json.Serialization;

namespace Altinn.Correspondence.Application.GenerateReport;

/// <summary>
/// Parquet-friendly model for per-notification daily summary data.
/// All properties are simple types optimized for ParquetSerializer.
/// </summary>
public class ParquetDailySummaryData
{
    /// <summary>
    /// Correspondence ID
    /// </summary>
    [JsonPropertyName("correspondenceid")]
    public string CorrespondenceId { get; set; } = string.Empty;

    /// <summary>
    /// Correspondence Created date (UTC calendar day) in YYYY-MM-DD format
    /// </summary>
    [JsonPropertyName("date")]
    public string Date { get; set; } = string.Empty;
    
    /// <summary>
    /// Year (YYYY)
    /// </summary>
    [JsonPropertyName("year")]
    public int Year { get; set; }
    
    /// <summary>
    /// Month (MM)
    /// </summary>
    [JsonPropertyName("month")]
    public int Month { get; set; }
    
    /// <summary>
    /// Day (DD)
    /// </summary>
    [JsonPropertyName("day")]
    public int Day { get; set; }
    
    /// <summary>
    /// Service Owner ID (organization number)
    /// </summary>
    [JsonPropertyName("serviceownerorgnr")]
    public string ServiceOwnerId { get; set; } = string.Empty;
    
    /// <summary>
    /// Service Owner Name, e.g. digdir, brreg, kv, etc.
    /// </summary>
    [JsonPropertyName("serviceownercode")]
    public string ServiceOwnerName { get; set; } = string.Empty;
    
    /// <summary>
    /// Message sender
    /// </summary>
    [JsonPropertyName("messagesender")]
    public string MessageSender { get; set; } = string.Empty;

    /// <summary>
    /// Sender organization number (if available in correspondence propertyList)
    /// </summary>
    [JsonPropertyName("senderorgnr")]
    public string SenderOrgNumber { get; set; } = string.Empty;
    
    /// <summary>
    /// Resource ID
    /// </summary>
    [JsonPropertyName("serviceresourceid")]
    public string ResourceId { get; set; } = string.Empty;
    
    /// <summary>
    /// Resource title in Norwegian (from Resource Registry)
    /// </summary>
    [JsonPropertyName("serviceresourcetitle")]
    public string ResourceTitle { get; set; } = string.Empty;
    
    /// <summary>
    /// Recipient type (Organization or Person)
    /// </summary>
    [JsonPropertyName("recipienttype")]
    public string RecipientType { get; set; } = string.Empty;
    
    /// <summary>
    /// Altinn version (Altinn2, Altinn3)
    /// </summary>
    [JsonPropertyName("costcenter")]
    public string AltinnVersion { get; set; } = string.Empty;
    
    /// <summary>
    /// Total database storage used (metadata) in bytes
    /// </summary>
    [JsonPropertyName("databasestoragebytes")]
    public long DatabaseStorageBytes { get; set; }
    
    /// <summary>
    /// Total attachment storage used in bytes
    /// </summary>
    [JsonPropertyName("attachmentstoragebytes")]
    public long AttachmentStorageBytes { get; set; }

    /// <summary>
    /// Notification shipment ID, if any
    /// </summary>
    [JsonPropertyName("shipment_id")]
    public string? ShipmentId { get; set; }

    /// <summary>
    /// Whether this row is a reminder notification. Null when the correspondence has no notifications.
    /// </summary>
    [JsonPropertyName("is_reminder")]
    public bool? IsReminder { get; set; }

    /// <summary>
    /// When the notification was sent (ISO 8601 UTC). Null when not sent yet or when the correspondence has no notifications.
    /// </summary>
    [JsonPropertyName("notification_sent")]
    public string? NotificationSent { get; set; }
}
