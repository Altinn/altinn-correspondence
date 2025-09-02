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
            // Get all correspondences with ServiceOwnerId data
            var correspondences = await correspondenceRepository.GetAllCorrespondencesForStatistics(cancellationToken);
            
            logger.LogInformation("Found {count} correspondences for statistics", correspondences.Count);

            // Create detailed report data for all correspondences
            var reportData = new List<CorrespondenceReportData>();
            var serviceOwnerIds = correspondences
                .Select(c => c.ServiceOwnerId)
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct()
                .ToList();

            // Get all service owner names in one go for efficiency
            var serviceOwnerNames = new Dictionary<string, string>();
            foreach (var serviceOwnerId in serviceOwnerIds)
            {
                try
                {
                    var serviceOwner = await serviceOwnerRepository.GetServiceOwnerByOrgNo(serviceOwnerId!, cancellationToken);
                    if (serviceOwner != null)
                    {
                        serviceOwnerNames[serviceOwnerId!] = serviceOwner.Name;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to get service owner name for ID: {serviceOwnerId}", serviceOwnerId);
                }
            }

            // Convert all correspondences to report data
            foreach (var correspondence in correspondences)
            {
                var serviceOwnerName = correspondence.ServiceOwnerId != null && serviceOwnerNames.ContainsKey(correspondence.ServiceOwnerId)
                    ? serviceOwnerNames[correspondence.ServiceOwnerId]
                    : null;

                reportData.Add(new CorrespondenceReportData
                {
                    CorrespondenceId = correspondence.Id.ToString(),
                    ServiceOwnerId = correspondence.ServiceOwnerId,
                    ServiceOwnerName = serviceOwnerName,
                    ResourceId = correspondence.ResourceId,
                    Sender = correspondence.Sender,
                    Recipient = correspondence.Recipient,
                    SendersReference = correspondence.SendersReference,
                    Created = correspondence.Created,
                    RequestedPublishTime = correspondence.RequestedPublishTime,
                    ReportDate = DateTimeOffset.UtcNow,
                    Environment = hostEnvironment.EnvironmentName,
                    ServiceOwnerMigrationStatus = correspondence.ServiceOwnerMigrationStatus
                });
            }

            logger.LogInformation("Generated detailed report with {correspondenceCount} correspondences for {serviceOwnerCount} service owners", 
                reportData.Count, serviceOwnerIds.Count);

            // Generate parquet file with detailed data
            var filePath = await GenerateParquetFile(reportData, cancellationToken);
            
            // Get file info
            var fileInfo = new FileInfo(filePath);
            
            var response = new GenerateStatisticsReportResponse
            {
                FilePath = filePath,
                ServiceOwnerCount = serviceOwnerIds.Count,
                TotalCorrespondenceCount = reportData.Count,
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
            return StatisticsErrors.ReportGenerationFailed;
        }
    }



    private async Task<string> GenerateParquetFile(List<CorrespondenceReportData> reportData, CancellationToken cancellationToken)
    {
        // Create reports directory if it doesn't exist
        var reportsDir = Path.Combine(Directory.GetCurrentDirectory(), "reports");
        Directory.CreateDirectory(reportsDir);

        // Generate filename with timestamp - using detailed naming for the new format
        var fileName = $"correspondence_detailed_report_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}_{hostEnvironment.EnvironmentName}.parquet";
        var filePath = Path.Combine(reportsDir, fileName);

        logger.LogInformation("Generating parquet file with {count} records to {filePath}", reportData.Count, filePath);

        // Convert to parquet-friendly model
        var parquetData = reportData.Select(r => new ParquetCorrespondenceData
        {
            CorrespondenceId = r.CorrespondenceId,
            ServiceOwnerId = r.ServiceOwnerId ?? string.Empty,
            ServiceOwnerName = r.ServiceOwnerName ?? string.Empty,
            ResourceId = r.ResourceId,
            Sender = r.Sender,
            Recipient = r.Recipient,
            SendersReference = r.SendersReference ?? string.Empty,
            Created = r.Created.ToString("O"), // ISO 8601 format
            RequestedPublishTime = r.RequestedPublishTime.ToString("O"),
            ReportDate = r.ReportDate.ToString("O"),
            Environment = r.Environment,
            ServiceOwnerMigrationStatus = r.ServiceOwnerMigrationStatus
        }).ToList();

        // Write parquet file with the simple model that works well with ParquetSerializer
        await ParquetSerializer.SerializeAsync(parquetData, filePath, cancellationToken: cancellationToken);

        logger.LogInformation("Successfully generated parquet file: {filePath}", filePath);
        return filePath;
    }
}
