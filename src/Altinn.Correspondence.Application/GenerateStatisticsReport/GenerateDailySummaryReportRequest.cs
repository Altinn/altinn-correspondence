namespace Altinn.Correspondence.Application.GenerateStatisticsReport;

/// <summary>
/// Request parameters for generating daily summary report
/// </summary>
public class GenerateDailySummaryReportRequest
{
    /// <summary>
    /// Whether to include Altinn2 correspondences in the report.
    /// If false, only Altinn3 correspondences will be included.
    /// Default is true (include both Altinn2 and Altinn3).
    /// </summary>
    public bool Altinn2Included { get; set; } = true;
}
