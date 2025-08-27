using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneOf;
using Parquet;
using Parquet.Serialization;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.GenerateStatisticsReport;

public class GenerateStatisticsReportHandler(
    ICorrespondenceRepository correspondenceRepository,
    IServiceOwnerRepository serviceOwnerRepository,
    IHostEnvironment hostEnvironment,
    ILogger<GenerateStatisticsReportHandler> logger) : IHandler<GenerateStatisticsReportResponse>
{
    public async Task<OneOf<GenerateStatisticsReportResponse, Error>> Process(ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting statistics report generation");

        try
        {
            // Get all correspondences and extract service owner IDs from Sender field
            var correspondences = await correspondenceRepository.GetAllCorrespondencesForStatistics(cancellationToken);
            
            logger.LogInformation("Found {count} correspondences for statistics", correspondences.Count);

            // Group by service owner ID (extract from Sender field)
            var serviceOwnerStats = new List<ServiceOwnerStatistics>();
            var groupedCorrespondences = correspondences
                .GroupBy(c => ExtractServiceOwnerIdFromSender(c.Sender))
                .Where(g => !string.IsNullOrEmpty(g.Key));

            foreach (var group in groupedCorrespondences)
            {
                var serviceOwnerId = group.Key!;
                var count = group.Count();
                
                // Try to get service owner name from database
                var serviceOwner = await serviceOwnerRepository.GetServiceOwnerByOrgNo(serviceOwnerId, cancellationToken);
                
                serviceOwnerStats.Add(new ServiceOwnerStatistics
                {
                    ServiceOwnerId = serviceOwnerId,
                    ServiceOwnerName = serviceOwner?.Name,
                    CorrespondenceCount = count,
                    ReportDate = DateTimeOffset.UtcNow,
                    Environment = hostEnvironment.EnvironmentName
                });
            }

            logger.LogInformation("Generated statistics for {serviceOwnerCount} service owners", serviceOwnerStats.Count);

            // Generate parquet file
            var filePath = await GenerateParquetFile(serviceOwnerStats, cancellationToken);
            
            // Get file info
            var fileInfo = new FileInfo(filePath);
            
            var response = new GenerateStatisticsReportResponse
            {
                FilePath = filePath,
                ServiceOwnerCount = serviceOwnerStats.Count,
                TotalCorrespondenceCount = serviceOwnerStats.Sum(s => s.CorrespondenceCount),
                GeneratedAt = DateTimeOffset.UtcNow,
                Environment = hostEnvironment.EnvironmentName,
                FileSizeBytes = fileInfo.Length
            };

            logger.LogInformation("Statistics report generated successfully at {filePath}", filePath);
            
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate statistics report");
            return new Error(
                ErrorCode: 500,
                Message: "Failed to generate statistics report",
                StatusCode: System.Net.HttpStatusCode.InternalServerError
            );
        }
    }

    private static string? ExtractServiceOwnerIdFromSender(string sender)
    {
        // Sender format: "0192:123456789" or "urn:altinn:organizationnumber:123456789"
        // We want to extract the organization number (the part after the last colon)
        if (string.IsNullOrEmpty(sender))
            return null;

        var lastColonIndex = sender.LastIndexOf(':');
        if (lastColonIndex >= 0 && lastColonIndex < sender.Length - 1)
        {
            return sender.Substring(lastColonIndex + 1);
        }

        return null;
    }

    private async Task<string> GenerateParquetFile(List<ServiceOwnerStatistics> statistics, CancellationToken cancellationToken)
    {
        // Create reports directory if it doesn't exist
        var reportsDir = Path.Combine(Directory.GetCurrentDirectory(), "reports");
        Directory.CreateDirectory(reportsDir);

        // Generate filename with timestamp
        var fileName = $"service_owner_statistics_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}_{hostEnvironment.EnvironmentName}.parquet";
        var filePath = Path.Combine(reportsDir, fileName);

        // Write parquet file
        await ParquetSerializer.SerializeAsync(statistics, filePath, cancellationToken: cancellationToken);

        return filePath;
    }
}
