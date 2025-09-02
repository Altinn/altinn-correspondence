namespace Altinn.Correspondence.Application.GenerateStatisticsReport;

/// <summary>
/// Parquet-friendly model for daily summary data.
/// All properties are simple types optimized for ParquetSerializer.
/// </summary>
public class ParquetDailySummaryData
{
    /// <summary>
    /// Date in YYYY-MM-DD format (as string for Parquet compatibility)
    /// </summary>
    public string Date { get; set; } = string.Empty;
    
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
    /// Service Owner Name
    /// </summary>
    public string ServiceOwnerName { get; set; } = string.Empty;
    
    /// <summary>
    /// Message sender
    /// </summary>
    public string MessageSender { get; set; } = string.Empty;
    
    /// <summary>
    /// Resource ID
    /// </summary>
    public string ResourceId { get; set; } = string.Empty;
    
    /// <summary>
    /// Number of messages/correspondences for this service owner on this date
    /// </summary>
    public int MessageCount { get; set; }
    
    /// <summary>
    /// Total database storage used (metadata) in bytes
    /// </summary>
    public long DatabaseStorageBytes { get; set; }
    
    /// <summary>
    /// Total attachment storage used in bytes
    /// </summary>
    public long AttachmentStorageBytes { get; set; }

}
