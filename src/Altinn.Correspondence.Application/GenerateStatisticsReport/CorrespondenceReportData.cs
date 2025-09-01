namespace Altinn.Correspondence.Application.GenerateStatisticsReport;

/// <summary>
/// Represents a single correspondence record in the detailed statistics report
/// </summary>
public class CorrespondenceReportData
{
    /// <summary>
    /// Unique identifier for the correspondence
    /// </summary>
    public string CorrespondenceId { get; set; } = string.Empty;
    
    /// <summary>
    /// Service Owner ID from the ServiceOwnerId field
    /// </summary>
    public string? ServiceOwnerId { get; set; }
    
    /// <summary>
    /// Service Owner Name (looked up from ServiceOwner table)
    /// </summary>
    public string? ServiceOwnerName { get; set; }
    
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
    public string? SendersReference { get; set; }
    
    /// <summary>
    /// When the correspondence was created
    /// </summary>
    public DateTimeOffset Created { get; set; }
    
    /// <summary>
    /// When the correspondence was requested to be published
    /// </summary>
    public DateTimeOffset RequestedPublishTime { get; set; }
    
    /// <summary>
    /// When this report was generated
    /// </summary>
    public DateTimeOffset ReportDate { get; set; }
    
    /// <summary>
    /// Environment (e.g., "Development", "Test", "Production")
    /// </summary>
    public string Environment { get; set; } = string.Empty;
    
    /// <summary>
    /// Service owner migration status for debugging
    /// 0: Not started/pending, 1: Completed with service owner, 2: Completed without service owner
    /// </summary>
    public int ServiceOwnerMigrationStatus { get; set; }
}
