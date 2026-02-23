using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneOf;
using Parquet.Serialization;
using System.Security.Cryptography;

namespace Altinn.Correspondence.Application.GenerateReport;

public class GenerateDailySummaryReportHandler(
    ICorrespondenceRepository correspondenceRepository,
    IServiceOwnerRepository serviceOwnerRepository,
    IResourceRegistryService resourceRegistryService,
    IStorageRepository storageRepository,
    ILogger<GenerateDailySummaryReportHandler> logger,
    IHostEnvironment hostEnvironment)
{
    public async Task<OneOf<GenerateDailySummaryReportResponse, Error>> Process(
        GenerateDailySummaryReportRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Starting daily summary report generation with Altinn2Included={altinn2Included}", request.Altinn2Included);

            // Get aggregated daily summary data directly from database
            var summaryDataDto = await correspondenceRepository.GetDailySummaryData(request.Altinn2Included, cancellationToken);
            logger.LogInformation("Retrieved {count} aggregated daily summary records from database", summaryDataDto.Count);

            if (summaryDataDto.Count == 0)
            {
                logger.LogWarning("No correspondences found for daily summary report generation");
                return StatisticsErrors.NoCorrespondencesFound;
            }

            // Map DTO to domain model and enrich with ResourceTitle
            var summaryData = await MapToDailySummaryData(summaryDataDto, cancellationToken);
            logger.LogInformation("Mapped and enriched data into {count} daily summary records", summaryData.Count);

            // Generate parquet file and upload to blob storage
            var totalCorrespondenceCount = summaryData.Sum(d => d.MessageCount);
            var (blobUrl, fileHash, fileSize) = await GenerateAndUploadParquetFile(summaryData, totalCorrespondenceCount, request.Altinn2Included, cancellationToken);

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

    private async Task<List<DailySummaryData>> MapToDailySummaryData(List<DailySummaryDataDto> dtoList, CancellationToken cancellationToken)
    {
        // Get unique resource IDs to fetch titles in bulk
        var resourceIds = dtoList
            .Where(d => !string.IsNullOrEmpty(d.ResourceId) && d.ResourceId != "unknown")
            .Select(d => d.ResourceId)
            .Distinct()
            .ToList();

        // Fetch resource titles in parallel (with error handling)
        var resourceTitleTasks = resourceIds.ToDictionary(
            resourceId => resourceId,
            resourceId => GetResourceTitleAsync(resourceId, cancellationToken)
        );

        await Task.WhenAll(resourceTitleTasks.Values);

        var resourceTitles = resourceTitleTasks.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Result
        );

        // Map DTO to domain model
        return dtoList.Select(dto => new DailySummaryData
        {
            Date = dto.Date,
            Year = dto.Year,
            Month = dto.Month,
            Day = dto.Day,
            ServiceOwnerId = dto.ServiceOwnerId,
            ServiceOwnerName = dto.ServiceOwnerName ?? GetServiceOwnerName(dto.ServiceOwnerId),
            MessageSender = dto.MessageSender,
            ResourceId = dto.ResourceId,
            ResourceTitle = resourceTitles.GetValueOrDefault(dto.ResourceId) ?? GetResourceTitle(dto.ResourceId),
            RecipientType = dto.RecipientType,
            AltinnVersion = dto.AltinnVersion,
            MessageCount = dto.MessageCount,
            DatabaseStorageBytes = dto.DatabaseStorageBytes,
            AttachmentStorageBytes = dto.AttachmentStorageBytes
        }).ToList();
    }

    private async Task<string> GetResourceTitleAsync(string? resourceId, CancellationToken cancellationToken)
    {
        try
        {
            var resourceTitle = await resourceRegistryService.GetServiceOwnerNameOfResource(resourceId, cancellationToken);
            return resourceTitle ?? $"Unknown ({resourceId})";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to get resource title for ID: {resourceId}", resourceId);
            return $"Error ({resourceId})";
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

    private DateTimeOffset GetDateTimeFromReportName(string reportName)
    {
        if (string.IsNullOrWhiteSpace(reportName))
        {
            throw new ArgumentException("Report name cannot be null or empty", nameof(reportName));
        }

        // Example: "20240127_143055_daily_summary_report_v2_production.parquet"
        var parts = reportName.Split('_');

        if (parts.Length < 2)
        {
            throw new FormatException($"Report name '{reportName}' does not contain expected datetime format");
        }

        // Combine date and time parts: "20240127" + "143055"
        var dateTimePart = $"{parts[0]}_{parts[1]}";

        if (DateTimeOffset.TryParseExact(
            dateTimePart,
            "yyyyMMdd_HHmmss",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeUniversal,
            out var result))
        {
            return result;
        }

        throw new FormatException($"Unable to parse datetime from report name '{reportName}'. Expected format: yyyyMMdd_HHmmss");
    }

    private async Task<(string blobUrl, string fileHash, long fileSize)> GenerateAndUploadParquetFile(List<DailySummaryData> summaryData, int correspondenceCount, bool altinn2Included, CancellationToken cancellationToken)
    {
        // Generate filename with timestamp as prefix and Altinn version indicator
        var altinnVersionIndicator = altinn2Included ? "A2A3" : "A3";
        var fileName = $"{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}_daily_summary_report_{altinnVersionIndicator}_{hostEnvironment.EnvironmentName}.parquet";

        logger.LogInformation("Generating daily summary parquet file with {count} records for blob storage", summaryData.Count);

        // Generate the parquet file as a stream
        var (parquetStream, fileHash, fileSize) = await GenerateParquetFileStream(summaryData, altinn2Included, cancellationToken);

        // Upload to blob storage
        var serviceOwnerCount = summaryData.Select(d => d.ServiceOwnerId).Distinct().Count();

        var (blobUrl, _, _) = await storageRepository.UploadReportFile(fileName, serviceOwnerCount, correspondenceCount, parquetStream, cancellationToken);

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

    public async Task<OneOf<GenerateAndDownloadDailySummaryReportResponse, Error>> DownloadReportFile(
        GenerateDailySummaryReportRequest request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting daily summary report generation and download with Altinn2Included={altinn2Included}", request.Altinn2Included);
        if (request.Altinn2Included)
        {
            logger.LogWarning("Download of daily summary report with Altinn2Included=true is not supported. Returning error.");
            return StatisticsErrors.Altinn2NotSupported;
        }

        try
        {
            // Get correspondences data
            var reportFile = await storageRepository.DownloadLatestReportFile(cancellationToken);

            var response = new GenerateAndDownloadDailySummaryReportResponse
            {
                FileStream = reportFile.DownloadStream,
                FileName = reportFile.FileName,
                FileHash = reportFile.FileHash,
                FileSizeBytes = reportFile.FileSize,
                ServiceOwnerCount = reportFile.ServiceOwnerCount,
                TotalCorrespondenceCount = reportFile.CorrespondenceCount,
                GeneratedAt = GetDateTimeFromReportName(reportFile.FileName),
                Environment = hostEnvironment.EnvironmentName,
                Altinn2Included = false // Always set to false, legacy
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

    public async Task<OneOf<GenerateAndDownloadDailySummaryReportResponse, Error>> ProcessAndDownload(
        GenerateDailySummaryReportRequest request,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting daily summary report generation and download with Altinn2Included={altinn2Included}", request.Altinn2Included);

        try
        {
            // Get aggregated daily summary data directly from database
            var summaryDataDto = await correspondenceRepository.GetDailySummaryData(request.Altinn2Included, cancellationToken);
            
            if (!summaryDataDto.Any())
            {
                logger.LogWarning("No correspondences found for report generation");
                return StatisticsErrors.NoCorrespondencesFound;
            }

            logger.LogInformation("Found {count} aggregated daily summary records", summaryDataDto.Count);

            // Map DTO to domain model and enrich with ResourceTitle
            var summaryData = await MapToDailySummaryData(summaryDataDto, cancellationToken);

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
                TotalCorrespondenceCount = summaryData.Sum(d => d.MessageCount),
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
