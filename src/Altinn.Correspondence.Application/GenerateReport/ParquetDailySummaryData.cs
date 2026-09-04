using System.Text.Json.Serialization;

namespace Altinn.Correspondence.Application.GenerateReport;

/// <summary>
/// Parquet-friendly model for per-correspondence daily summary data.
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
    /// Date in YYYY-MM-DD format (as string for Parquet compatibility)
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
    /// Always 1 for per-correspondence rows (kept for backward compatibility)
    /// </summary>
    [JsonPropertyName("messagecount")]
    public int MessageCount { get; set; } = 1;
    
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
    /// Shipment ID of the main notification, if any
    /// </summary>
    [JsonPropertyName("shipment_id")]
    public string? ShipmentId { get; set; }

    /// <summary>
    /// Shipment ID of the reminder notification, if any
    /// </summary>
    [JsonPropertyName("reminder_shipment_id")]
    public string? ReminderShipmentId { get; set; }
}
