namespace Altinn.Correspondence.Application.GenerateReport;

/// <summary>
/// Request parameters for generating a monthly daily-summary report.
/// </summary>
public class GenerateDailySummaryReportRequest
{
    /// <summary>
    /// Whether to include Altinn2 correspondences in the report.
    /// If false, only Altinn3 correspondences will be included.
    /// </summary>
    public bool Altinn2Included { get; set; } = false;

    /// <summary>
    /// Optional report year (UTC). When omitted together with <see cref="Month"/>, the current UTC month is used.
    /// Must be set together with <see cref="Month"/> for backfill of a specific month.
    /// </summary>
    public int? Year { get; set; }

    /// <summary>
    /// Optional report month 1-12 (UTC). When omitted together with <see cref="Year"/>, the current UTC month is used.
    /// Must be set together with <see cref="Year"/> for backfill of a specific month.
    /// </summary>
    public int? Month { get; set; }
}
