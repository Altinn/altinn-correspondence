namespace Altinn.Correspondence.Application.GenerateReport;

public class GenerateAndDownloadDailySummaryReportResponse
{
    /// <summary>
    /// The parquet file stream
    /// </summary>
    public required Stream FileStream { get; set; }
    
    /// <summary>
    /// The filename for the download
    /// </summary>
    public required string FileName { get; set; }
    
    /// <summary>
    /// MD5 hash of the file
    /// </summary>
    public required string FileHash { get; set; }
    
    /// <summary>
    /// File size in bytes
    /// </summary>
    public long FileSizeBytes { get; set; }
    
    /// <summary>
    /// Number of service owners included in the report
    /// </summary>
    public int ServiceOwnerCount { get; set; }
    
    /// <summary>
    /// Total number of correspondences included in the report
    /// </summary>
    public int TotalCorrespondenceCount { get; set; }
    
    /// <summary>
    /// When the report was generated (UTC)
    /// </summary>
    public DateTimeOffset GeneratedAt { get; set; }
    
    /// <summary>
    /// Environment where the report was generated
    /// </summary>
    public required string Environment { get; set; }
    
    /// <summary>
    /// Whether Altinn2 correspondences were included
    /// </summary>
    public bool Altinn2Included { get; set; }
}
