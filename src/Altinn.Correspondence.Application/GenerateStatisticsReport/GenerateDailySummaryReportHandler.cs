using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OneOf;
using Parquet.Serialization;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.GenerateStatisticsReport;

public class GenerateDailySummaryReportHandler(
    ICorrespondenceRepository correspondenceRepository,
    IServiceOwnerRepository serviceOwnerRepository,
    ILogger<GenerateDailySummaryReportHandler> logger,
    IHostEnvironment hostEnvironment)
{
    public async Task<OneOf<GenerateStatisticsReportResponse, Error>> Process(
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Starting daily summary report generation");

            // Get all correspondences for statistics
            var correspondences = await correspondenceRepository.GetAllCorrespondencesForStatistics(cancellationToken);
            logger.LogInformation("Retrieved {count} correspondences for daily summary report", correspondences.Count);

            if (correspondences.Count == 0)
            {
                logger.LogWarning("No correspondences found for daily summary report generation");
                return StatisticsErrors.NoCorrespondencesFound;
            }

            // Aggregate daily data
            var summaryData = AggregateDailyData(correspondences);
            logger.LogInformation("Aggregated data into {count} daily summary records", summaryData.Count);

            // Generate parquet file
            var filePath = await GenerateParquetFile(summaryData, cancellationToken);
            var fileInfo = new FileInfo(filePath);

            var response = new GenerateStatisticsReportResponse
            {
                FilePath = filePath,
                ServiceOwnerCount = summaryData.Select(d => d.ServiceOwnerId).Distinct().Count(),
                TotalCorrespondenceCount = summaryData.Sum(d => d.MessageCount),
                GeneratedAt = DateTimeOffset.UtcNow,
                Environment = hostEnvironment.EnvironmentName ?? "Unknown",
                FileSizeBytes = fileInfo.Length
            };

            logger.LogInformation("Successfully generated daily summary report: {filePath}", filePath);
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
                RecipientType = GetRecipientType(c.Recipient)
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
                RecipientType = g.Key.RecipientType,
                MessageCount = g.Count(),
                DatabaseStorageBytes = CalculateDatabaseStorage(g.ToList()),
                AttachmentStorageBytes = CalculateAttachmentStorage(g.ToList())
            })
            .OrderBy(d => d.Date)
            .ThenBy(d => d.ServiceOwnerId)
            .ThenBy(d => d.MessageSender)
            .ThenBy(d => d.ResourceId)
            .ThenBy(d => d.RecipientType)
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

    private long CalculateDatabaseStorage(List<CorrespondenceEntity> correspondences)
    {
        // Estimate database storage based on correspondence metadata
        // This is a rough estimate - you might want to calculate actual storage
        const long estimatedBytesPerCorrespondence = 1024; // 1KB per correspondence metadata
        return correspondences.Count * estimatedBytesPerCorrespondence;
    }

    private long CalculateAttachmentStorage(List<CorrespondenceEntity> correspondences)
    {
        // TODO: Calculate actual attachment storage from AttachmentEntity
        // For now, return 0 as we don't have attachment data in the current query
        return 0;
    }

    private async Task<string> GenerateParquetFile(List<DailySummaryData> summaryData, CancellationToken cancellationToken)
    {
        // Create reports directory if it doesn't exist
        var reportsDir = Path.Combine(Directory.GetCurrentDirectory(), "reports");
        Directory.CreateDirectory(reportsDir);

        // Generate filename with timestamp
        var fileName = $"daily_summary_report_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}_{hostEnvironment.EnvironmentName}.parquet";
        var filePath = Path.Combine(reportsDir, fileName);

        logger.LogInformation("Generating daily summary parquet file with {count} records to {filePath}", summaryData.Count, filePath);

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
            RecipientType = d.RecipientType.ToString(),
            MessageCount = d.MessageCount,
            DatabaseStorageBytes = d.DatabaseStorageBytes,
            AttachmentStorageBytes = d.AttachmentStorageBytes
        }).ToList();

        // Write parquet file
        await ParquetSerializer.SerializeAsync(parquetData, filePath, cancellationToken: cancellationToken);

        logger.LogInformation("Successfully generated daily summary parquet file: {filePath}", filePath);

        return filePath;
    }
}
