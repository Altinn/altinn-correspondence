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
}
