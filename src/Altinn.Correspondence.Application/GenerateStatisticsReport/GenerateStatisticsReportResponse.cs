namespace Altinn.Correspondence.Application.GenerateStatisticsReport;

public class GenerateStatisticsReportResponse
{
    /// <summary>
    /// URL to the generated parquet file in blob storage
    /// </summary>
    public required string FilePath { get; set; }
    
    /// <summary>
    /// Number of service owners included in the report
    /// </summary>
    public int ServiceOwnerCount { get; set; }
    
    /// <summary>
    /// Total number of correspondences included in the report
    /// </summary>
    public int TotalCorrespondenceCount { get; set; }
    
    /// <summary>
    /// Report generation timestamp
    /// </summary>
    public DateTimeOffset GeneratedAt { get; set; }
    
    /// <summary>
    /// Environment (e.g., "test", "production")
    /// </summary>
    public required string Environment { get; set; }
    
    /// <summary>
    /// File size in bytes
    /// </summary>
    public long FileSizeBytes { get; set; }
}
