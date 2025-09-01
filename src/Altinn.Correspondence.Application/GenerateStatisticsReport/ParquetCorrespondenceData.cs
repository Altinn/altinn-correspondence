namespace Altinn.Correspondence.Application.GenerateStatisticsReport;

/// <summary>
/// Simple model optimized for Parquet serialization/deserialization.
/// All properties are simple types that work well with ParquetSerializer.
/// </summary>
public class ParquetCorrespondenceData
{
    /// <summary>
    /// Unique identifier for the correspondence
    /// </summary>
    public string CorrespondenceId { get; set; } = string.Empty;
    
    /// <summary>
    /// Service Owner ID
    /// </summary>
    public string ServiceOwnerId { get; set; } = string.Empty;
    
    /// <summary>
    /// Service Owner Name
    /// </summary>
    public string ServiceOwnerName { get; set; } = string.Empty;
    
    /// <summary>
    /// Resource ID for the correspondence
    /// </summary>
    public string ResourceId { get; set; } = string.Empty;
    
    /// <summary>
    /// Correspondence sender (URN format)
    /// </summary>
    public string Sender { get; set; } = string.Empty;
    
    /// <summary>
    /// Correspondence recipient
    /// </summary>
    public string Recipient { get; set; } = string.Empty;
    
    /// <summary>
    /// Sender's reference for the correspondence
    /// </summary>
    public string SendersReference { get; set; } = string.Empty;
    
    /// <summary>
    /// When the correspondence was created (as string for Parquet compatibility)
    /// </summary>
    public string Created { get; set; } = string.Empty;
    
    /// <summary>
    /// When the correspondence was requested to be published (as string for Parquet compatibility)
    /// </summary>
    public string RequestedPublishTime { get; set; } = string.Empty;
    
    /// <summary>
    /// When this report was generated (as string for Parquet compatibility)
    /// </summary>
    public string ReportDate { get; set; } = string.Empty;
    
    /// <summary>
    /// Environment (e.g., "Development", "Test", "Production")
    /// </summary>
    public string Environment { get; set; } = string.Empty;
    
    /// <summary>
    /// Service owner migration status for debugging
    /// </summary>
    public int ServiceOwnerMigrationStatus { get; set; }
}
