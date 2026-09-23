namespace Altinn.Correspondence.Application.GenerateReport;

public class EnqueueDailySummaryReportResponse
{
    /// <summary>
    /// Hangfire job id for the enqueued report generation.
    /// </summary>
    public required string JobId { get; set; }

    /// <summary>
    /// Human-readable status message.
    /// </summary>
    public required string Message { get; set; }

    /// <summary>
    /// Indicates if Altinn2 correspondences will be included in the report.
    /// </summary>
    public required bool Altinn2Included { get; set; }

    /// <summary>
    /// UTC year of the report being generated.
    /// </summary>
    public required int Year { get; set; }

    /// <summary>
    /// UTC month (1-12) of the report being generated.
    /// </summary>
    public required int Month { get; set; }

    /// <summary>
    /// UTC day (1-31) when generating a single-day report; null for monthly reports.
    /// </summary>
    public int? Day { get; set; }
}
