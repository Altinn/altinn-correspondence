using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneOf;
using Parquet.Serialization;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Altinn.Correspondence.Application.GenerateStatisticsReport;

public class GenerateDailySummaryReportHandler(
    ICorrespondenceRepository correspondenceRepository,
    IServiceOwnerRepository serviceOwnerRepository,
    IResourceRegistryService resourceRegistryService,
    IStorageRepository storageRepository,
    ILogger<GenerateDailySummaryReportHandler> logger,
    IHostEnvironment hostEnvironment)
{
    public async Task<OneOf<GenerateDailySummaryReportResponse, Error>> Process(
        ClaimsPrincipal user,
        GenerateDailySummaryReportRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Starting daily summary report generation with Altinn2Included={altinn2Included}", request.Altinn2Included);

            // Get correspondences for statistics with filtering
            var correspondences = await correspondenceRepository.GetCorrespondencesForStatistics(request.Altinn2Included, cancellationToken);
            logger.LogInformation("Retrieved {count} correspondences for daily summary report", correspondences.Count);

            if (correspondences.Count == 0)
            {
                logger.LogWarning("No correspondences found for daily summary report generation");
                return StatisticsErrors.NoCorrespondencesFound;
            }

            // Aggregate daily data
            var summaryData = AggregateDailyData(correspondences);
            logger.LogInformation("Aggregated data into {count} daily summary records", summaryData.Count);

            // Generate parquet file and upload to blob storage
            var (blobUrl, fileHash, fileSize) = await GenerateAndUploadParquetFile(summaryData, request.Altinn2Included, cancellationToken);

            var response = new GenerateDailySummaryReportResponse
            {
                FilePath = blobUrl, // Now contains the blob storage URL
                ServiceOwnerCount = summaryData.Select(d => d.ServiceOwnerId).Distinct().Count(),
                TotalCorrespondenceCount = summaryData.Sum(d => d.MessageCount),
                GeneratedAt = DateTimeOffset.UtcNow,
                Environment = hostEnvironment.EnvironmentName ?? "Unknown",
                FileSizeBytes = fileSize,
                FileHash = fileHash,
                Altinn2Included = request.Altinn2Included
            };

            logger.LogInformation("Successfully generated and uploaded daily summary report to blob storage: {blobUrl}", blobUrl);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate daily summary report");
            return StatisticsErrors.ReportGenerationFailed;
        }
    }

    private List<DailySummaryData> AggregateDailyData(List<CorrespondenceEntity> correspondences)
    {
        var groupedData = correspondences
            .GroupBy(c => new
            {
                c.Created.Date, // Parse DateTimeOffset to Date for grouping
                ServiceOwnerId = c.ServiceOwnerId ?? "unknown",
                MessageSender = string.IsNullOrEmpty(c.MessageSender) ? "unknown" : c.MessageSender,
                ResourceId = string.IsNullOrEmpty(c.ResourceId) ? "unknown" : c.ResourceId,
                RecipientType = GetRecipientType(c.Recipient),
                AltinnVersion = GetAltinnVersion(c.Altinn2CorrespondenceId)
            })
            .Select(g => new DailySummaryData
            {
                Date = g.Key.Date,
                Year = g.Key.Date.Year,
                Month = g.Key.Date.Month,
                Day = g.Key.Date.Day,
                ServiceOwnerId = g.Key.ServiceOwnerId,
                ServiceOwnerName = GetServiceOwnerName(g.Key.ServiceOwnerId),
                MessageSender = g.Key.MessageSender,
                ResourceId = g.Key.ResourceId,
                ResourceTitle = GetResourceTitle(g.Key.ResourceId),
                RecipientType = g.Key.RecipientType,
                AltinnVersion = g.Key.AltinnVersion,
                MessageCount = g.Count(),
                DatabaseStorageBytes = CalculateDatabaseStorage(g.ToList()),
                AttachmentStorageBytes = CalculateAttachmentStorage(g.ToList())
            })
            .OrderBy(d => d.Date)
            .ThenBy(d => d.ServiceOwnerId)
            .ThenBy(d => d.MessageSender)
            .ThenBy(d => d.ResourceId)
            .ThenBy(d => d.RecipientType)
            .ThenBy(d => d.AltinnVersion)
            .ToList();

        return groupedData;
    }

    private RecipientType GetRecipientType(string recipient)
    {
        if (string.IsNullOrEmpty(recipient))
        {
            return RecipientType.Unknown;
        }

        string recipientWithoutPrefix = recipient.WithoutPrefix();
        bool isOrganization = recipientWithoutPrefix.IsOrganizationNumber();
        bool isPerson = recipientWithoutPrefix.IsSocialSecurityNumber();

        if (isOrganization)
        {
            return RecipientType.Organization;
        }
        else if (isPerson)
        {
            return RecipientType.Person;
        }
        else
        {
            return RecipientType.Unknown; // For invalid or unrecognized formats
        }
    }

    private AltinnVersion GetAltinnVersion(int? altinn2CorrespondenceId)
    {
        return altinn2CorrespondenceId.HasValue ? AltinnVersion.Altinn2 : AltinnVersion.Altinn3;
    }

    private string GetServiceOwnerName(string? serviceOwnerId)
    {
        if (string.IsNullOrEmpty(serviceOwnerId))
        {
            return "Unknown";
        }

        try
        {
            // ServiceOwnerId is the organization number, which is the Id in ServiceOwners table
            var serviceOwner = serviceOwnerRepository.GetServiceOwnerByOrgNo(serviceOwnerId, CancellationToken.None).GetAwaiter().GetResult();
            return serviceOwner?.Name ?? $"Unknown ({serviceOwnerId})";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get service owner name for ID: {serviceOwnerId}", serviceOwnerId);
            return $"Error ({serviceOwnerId})";
        }
    }

    private string GetResourceTitle(string? resourceId)
    {
        if (string.IsNullOrEmpty(resourceId) || resourceId == "unknown")
        {
            return "Unknown";
        }

        try
        {
            var resourceTitle = resourceRegistryService.GetServiceOwnerNameOfResource(resourceId, CancellationToken.None).GetAwaiter().GetResult();
            return resourceTitle ?? $"Unknown ({resourceId})";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get resource title for ID: {resourceId}", resourceId);
            return $"Error ({resourceId})";
        }
    }

    private long CalculateDatabaseStorage(List<CorrespondenceEntity> correspondences)
    {
        // TODO: Calculate atabase storage based on correspondence metadata
        // For now, return 0 as placeholder
        return 0;
    }

    private long CalculateAttachmentStorage(List<CorrespondenceEntity> correspondences)
    {
        // TODO: Calculate actual attachment storage from AttachmentEntity
        // For now, return 0 as placeholder
        return 0;
    }

    private async Task<(string blobUrl, string fileHash, long fileSize)> GenerateAndUploadParquetFile(List<DailySummaryData> summaryData, bool altinn2Included, CancellationToken cancellationToken)
    {
        // Generate filename with timestamp as prefix and Altinn version indicator
        var altinnVersionIndicator = altinn2Included ? "A2A3" : "A3";
        var fileName = $"{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}_daily_summary_report_{altinnVersionIndicator}_{hostEnvironment.EnvironmentName}.parquet";

        logger.LogInformation("Generating daily summary parquet file with {count} records for blob storage", summaryData.Count);

        // Generate the parquet file as a stream
        var (parquetStream, fileHash, fileSize) = await GenerateParquetFileStream(summaryData, altinn2Included, cancellationToken);

        // Upload to blob storage
        var (blobUrl, _, _) = await storageRepository.UploadReportFile(fileName, parquetStream, cancellationToken);

        logger.LogInformation("Successfully generated and uploaded daily summary parquet file to blob storage: {blobUrl}", blobUrl);

        return (blobUrl, fileHash, fileSize);
    }

    private async Task<(Stream parquetStream, string fileHash, long fileSize)> GenerateParquetFileStream(List<DailySummaryData> summaryData, bool altinn2Included, CancellationToken cancellationToken)
    {
        // Generate filename with timestamp as prefix and Altinn version indicator
        var altinnVersionIndicator = altinn2Included ? "A2A3" : "A3";
        var fileName = $"{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}_daily_summary_report_{altinnVersionIndicator}_{hostEnvironment.EnvironmentName}.parquet";

        logger.LogInformation("Generating daily summary parquet file with {count} records", summaryData.Count);

        // Convert to parquet-friendly model
        var parquetData = summaryData.Select(d => new ParquetDailySummaryData
        {
            Date = d.Date.ToString("yyyy-MM-dd"),
            Year = d.Year,
            Month = d.Month,
            Day = d.Day,
            ServiceOwnerId = d.ServiceOwnerId,
            ServiceOwnerName = d.ServiceOwnerName,
            MessageSender = d.MessageSender,
            ResourceId = d.ResourceId,
            ResourceTitle = d.ResourceTitle,
            RecipientType = d.RecipientType.ToString(),
            AltinnVersion = d.AltinnVersion.ToString(),
            MessageCount = d.MessageCount,
            DatabaseStorageBytes = d.DatabaseStorageBytes,
            AttachmentStorageBytes = d.AttachmentStorageBytes
        }).ToList();

        // Create a memory stream for the parquet data
        var memoryStream = new MemoryStream();
        
        // Write parquet data to memory stream
        await ParquetSerializer.SerializeAsync(parquetData, memoryStream, cancellationToken: cancellationToken);
        memoryStream.Position = 0; // Reset position for reading

        // Calculate MD5 hash
        using var md5 = MD5.Create();
        var hash = Convert.ToBase64String(md5.ComputeHash(memoryStream.ToArray()));
        memoryStream.Position = 0; // Reset position for reading

        logger.LogInformation("Successfully generated daily summary parquet file stream");

        return (memoryStream, hash, memoryStream.Length);
    }

    public async Task<OneOf<GenerateAndDownloadDailySummaryReportResponse, Error>> ProcessAndDownload(
        ClaimsPrincipal user,
        GenerateDailySummaryReportRequest request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting daily summary report generation and download with Altinn2Included={altinn2Included}", request.Altinn2Included);

        try
        {
            // Get correspondences data
            var correspondences = await correspondenceRepository.GetCorrespondencesForStatistics(request.Altinn2Included, cancellationToken);
            
            if (!correspondences.Any())
            {
                logger.LogWarning("No correspondences found for report generation");
                return StatisticsErrors.NoCorrespondencesFound;
            }

            logger.LogInformation("Found {count} correspondences for report generation", correspondences.Count);

            // Aggregate data by day and service owner
            var summaryData = AggregateDailyData(correspondences);

            // Generate the parquet file as a stream
            var (parquetStream, fileHash, fileSize) = await GenerateParquetFileStream(summaryData, request.Altinn2Included, cancellationToken);

            // Generate filename
            var altinnVersionIndicator = request.Altinn2Included ? "A2A3" : "A3";
            var fileName = $"{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}_daily_summary_report_{altinnVersionIndicator}_{hostEnvironment.EnvironmentName}.parquet";

            var response = new GenerateAndDownloadDailySummaryReportResponse
            {
                FileStream = parquetStream,
                FileName = fileName,
                FileHash = fileHash,
                FileSizeBytes = fileSize,
                ServiceOwnerCount = summaryData.Select(d => d.ServiceOwnerId).Distinct().Count(),
                TotalCorrespondenceCount = correspondences.Count,
                GeneratedAt = DateTimeOffset.UtcNow,
                Environment = hostEnvironment.EnvironmentName,
                Altinn2Included = request.Altinn2Included
            };

            logger.LogInformation("Successfully generated daily summary report for download with {serviceOwnerCount} service owners and {totalCount} correspondences", 
                response.ServiceOwnerCount, response.TotalCorrespondenceCount);

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate daily summary report for download");
            return StatisticsErrors.ReportGenerationFailed;
        }
    }
}
