using Altinn.Correspondence.Core.Repositories;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.DownloadStatisticsReport;

public class DownloadStatisticsReportHandler(
    IStorageRepository storageRepository,
    ILogger<DownloadStatisticsReportHandler> logger) : IHandler<DownloadStatisticsReportRequest, Stream>
{
    public async Task<OneOf<Stream, Error>> Process(DownloadStatisticsReportRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing download request for statistics report {FileName}", request.FileName);

        try
        {
            // Basic validation to prevent directory traversal and ensure it's a parquet file
            if (string.IsNullOrEmpty(request.FileName) || 
                request.FileName.Contains("..") || 
                request.FileName.Contains("/") || 
                request.FileName.Contains("\\") ||
                !request.FileName.EndsWith(".parquet", StringComparison.OrdinalIgnoreCase))
            {
                logger.LogWarning("Invalid file name requested: {FileName}", request.FileName);
                return StatisticsErrors.InvalidFileName;
            }

            // Download the file from blob storage
            // Note: We need to add a method to IStorageRepository for downloading reports
            var reportStream = await storageRepository.DownloadReportFile(request.FileName, cancellationToken);
            
            logger.LogInformation("Successfully downloaded statistics report {FileName}", request.FileName);
            return reportStream;
        }
        catch (FileNotFoundException)
        {
            logger.LogWarning("Statistics report not found: {FileName}", request.FileName);
            return StatisticsErrors.ReportNotFound;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error downloading statistics report {FileName}", request.FileName);
            return StatisticsErrors.ReportDownloadFailed;
        }
    }
}
