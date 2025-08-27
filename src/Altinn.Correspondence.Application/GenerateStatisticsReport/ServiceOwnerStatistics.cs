namespace Altinn.Correspondence.Application.GenerateStatisticsReport;

public class ServiceOwnerStatistics
{
    /// <summary>
    /// Service Owner ID (extracted from Sender field)
    /// </summary>
    public required string ServiceOwnerId { get; set; }
    
    /// <summary>
    /// Service Owner Name
    /// </summary>
    public string? ServiceOwnerName { get; set; }
    
    /// <summary>
    /// Number of correspondences for this service owner
    /// </summary>
    public int CorrespondenceCount { get; set; }
    
    /// <summary>
    /// Report generation date
    /// </summary>
    public DateTimeOffset ReportDate { get; set; }
    
    /// <summary>
    /// Environment (e.g., "test", "production")
    /// </summary>
    public required string Environment { get; set; }
}
