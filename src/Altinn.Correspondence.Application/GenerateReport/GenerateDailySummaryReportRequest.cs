namespace Altinn.Correspondence.Application.GenerateReport;

/// <summary>
/// Request parameters for generating or downloading a daily-summary report (UTC month or UTC day).
/// </summary>
public class GenerateDailySummaryReportRequest
{
    /// <summary>
    /// Whether to include Altinn2 correspondences in the report.
    /// If false, only Altinn3 correspondences will be included.
    /// </summary>
    public bool Altinn2Included { get; set; } = false;

    /// <summary>
    /// Optional report year (UTC). When omitted together with <see cref="Month"/> and <see cref="Day"/>,
    /// the preceding UTC day is used. Must be set together with <see cref="Month"/> when specifying a period.
    /// </summary>
    public int? Year { get; set; }

    /// <summary>
    /// Optional report month 1-12 (UTC). When omitted together with <see cref="Year"/> and <see cref="Day"/>,
    /// the preceding UTC day is used. Must be set together with <see cref="Year"/> when specifying a period.
    /// </summary>
    public int? Month { get; set; }

    /// <summary>
    /// Optional report day 1-31 (UTC). When set together with <see cref="Year"/> and <see cref="Month"/>,
    /// a single-day report is generated/downloaded instead of a monthly report.
    /// Must be before the current UTC day (today and future dates are rejected).
    /// When year/month/day are all omitted, the preceding UTC day is used by default.
    /// </summary>
    public int? Day { get; set; }
}
