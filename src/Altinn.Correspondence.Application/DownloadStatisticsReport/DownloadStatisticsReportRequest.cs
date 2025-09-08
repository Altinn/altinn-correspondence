namespace Altinn.Correspondence.Application.DownloadStatisticsReport;

public class DownloadStatisticsReportRequest
{
    /// <summary>
    /// The filename of the report to download
    /// </summary>
    public required string FileName { get; set; }
}
