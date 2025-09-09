namespace Altinn.Correspondence.Application.GenerateReport;

public class GenerateDailySummaryReportResponse
{
    /// <summary>
    /// URL to the generated parquet file in blob storage.
    /// </summary>
    public required string FilePath { get; set; }

    /// <summary>
    /// MD5 hash of the generated file.
    /// </summary>
    public required string FileHash { get; set; }

    /// <summary>
    /// Size of the generated file in bytes.
    /// </summary>
    public required long FileSizeBytes { get; set; }

    /// <summary>
    /// Number of unique service owners included in the report.
    /// </summary>
    public required int ServiceOwnerCount { get; set; }

    /// <summary>
    /// Total number of correspondences included in the report.
    /// </summary>
    public required int TotalCorrespondenceCount { get; set; }

    /// <summary>
    /// Timestamp when the report was generated.
    /// </summary>
    public required DateTimeOffset GeneratedAt { get; set; }

    /// <summary>
    /// The environment the report was generated for.
    /// </summary>
    public required string Environment { get; set; }

    /// <summary>
    /// Indicates if Altinn2 correspondences were included in the report.
    /// </summary>
    public required bool Altinn2Included { get; set; }
}
