using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Hangfire;
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
    IBackgroundJobClient backgroundJobClient,
    ILogger<GenerateDailySummaryReportHandler> logger,
    IHostEnvironment hostEnvironment)
{
    /// <summary>
    /// Enqueues report generation as a Hangfire background job and returns immediately.
    /// Defaults to the current UTC month; optional Year/Month on the request allow backfill.
    /// Use download endpoints to fetch the parquet file after the job completes.
    /// </summary>
    public Task<OneOf<EnqueueDailySummaryReportResponse, Error>> Process(
        GenerateDailySummaryReportRequest request,
        CancellationToken cancellationToken)
    {
        int year;
        int month;
        try
        {
            (year, month) = ResolveReportMonth(request);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid report month in generate request");
            return Task.FromResult<OneOf<EnqueueDailySummaryReportResponse, Error>>(StatisticsErrors.InvalidReportMonth);
        }

        logger.LogInformation(
            "Enqueueing monthly daily summary report generation for {Year}-{Month:D2} with Altinn2Included={altinn2Included}",
            year,
            month,
            request.Altinn2Included);

        var jobId = backgroundJobClient.Enqueue(() =>
            ExecuteInBackground(request.Altinn2Included, year, month, CancellationToken.None));

        logger.LogInformation(
            "Daily summary report generation job {JobId} has been enqueued for {Year}-{Month:D2}",
            jobId,
            year,
            month);

        return Task.FromResult<OneOf<EnqueueDailySummaryReportResponse, Error>>(new EnqueueDailySummaryReportResponse
        {
            JobId = jobId,
            Message = $"Monthly daily summary report generation for {year}-{month:D2} has been enqueued. Use the download endpoint when the job has completed.",
            Altinn2Included = request.Altinn2Included,
            Year = year,
            Month = month
        });
    }

    /// <summary>
    /// Regenerates only the current UTC month. Used by the daily Hangfire recurring job
    /// so older monthly reports remain unchanged.
    /// </summary>
    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(timeoutInSeconds: 14400)]
    public Task ExecuteCurrentMonthInBackground(bool altinn2Included, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        return ExecuteInBackground(altinn2Included, now.Year, now.Month, cancellationToken);
    }

    /// <summary>
    /// Performs report generation and upload for a single UTC month.
    /// Invoked by Hangfire (API enqueue or recurring job). Older months are left unchanged.
    /// </summary>
    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(timeoutInSeconds: 14400)]
    public async Task ExecuteInBackground(bool altinn2Included, int year, int month, CancellationToken cancellationToken)
    {
        var (fromInclusive, toExclusive) = GetUtcMonthRange(year, month);
        logger.LogInformation(
            "Starting monthly daily summary report generation for {Year}-{Month:D2} (range [{From}, {To})) Altinn2Included={altinn2Included}",
            year,
            month,
            fromInclusive,
            toExclusive,
            altinn2Included);

        try
        {
            var summaryDataDto = await correspondenceRepository.GetDailySummaryData(
                altinn2Included,
                fromInclusive,
                toExclusive,
                cancellationToken);
            logger.LogInformation(
                "Retrieved {count} correspondence summary records for {Year}-{Month:D2}",
                summaryDataDto.Count,
                year,
                month);

            if (summaryDataDto.Count == 0)
            {
                logger.LogWarning("No correspondences found for monthly daily summary report {Year}-{Month:D2}", year, month);
                return;
            }

            var summaryData = await MapToDailySummaryData(summaryDataDto, cancellationToken);
            logger.LogInformation("Mapped and enriched data into {count} correspondence summary records", summaryData.Count);

            var totalCorrespondenceCount = summaryData.Count;
            var (blobUrl, _, _) = await GenerateAndUploadParquetFile(
                summaryData,
                totalCorrespondenceCount,
                altinn2Included,
                year,
                month,
                cancellationToken);

            logger.LogInformation(
                "Successfully generated and uploaded monthly daily summary report for {Year}-{Month:D2} to blob storage: {blobUrl}",
                year,
                month,
                blobUrl);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate monthly daily summary report for {Year}-{Month:D2}", year, month);
            throw;
        }
    }

    public static (int Year, int Month) ResolveReportMonth(GenerateDailySummaryReportRequest request)
    {
        if (request.Year is null && request.Month is null)
        {
            var now = DateTimeOffset.UtcNow;
            return (now.Year, now.Month);
        }

        if (request.Year is null || request.Month is null)
        {
            throw new ArgumentException("Year and Month must both be provided when specifying a report month.");
        }

        if (request.Month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(request.Month), request.Month, "Month must be between 1 and 12.");
        }

        if (request.Year < 2025)
        {
            throw new ArgumentOutOfRangeException(nameof(request.Year), request.Year, "Year must be 2025 or later.");
        }

        return (request.Year.Value, request.Month.Value);
    }

    public static (DateTimeOffset FromInclusive, DateTimeOffset ToExclusive) GetUtcMonthRange(int year, int month)
    {
        var fromInclusive = new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero);
        return (fromInclusive, fromInclusive.AddMonths(1));
    }

    public static string BuildMonthlyReportFileName(int year, int month, bool altinn2Included, string environmentName)
    {
        var altinnVersionIndicator = altinn2Included ? "A2A3" : "A3";
        return $"daily_summary_report_{year:D4}{month:D2}_{altinnVersionIndicator}_{environmentName}.parquet";
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
            resourceId => GetResourceTitle(resourceId, cancellationToken)
        );

        await Task.WhenAll(resourceTitleTasks.Values);

        var resourceTitles = resourceTitleTasks.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value.Result
        );

        // Map DTO to domain model
        return dtoList.Select(dto => new DailySummaryData
        {
            CorrespondenceId = dto.CorrespondenceId,
            Date = dto.Date,
            Year = dto.Year,
            Month = dto.Month,
            Day = dto.Day,
            ServiceOwnerId = dto.ServiceOwnerId,
            ServiceOwnerName = dto.ServiceOwnerName ?? GetServiceOwnerName(dto.ServiceOwnerId),
            MessageSender = dto.MessageSender,
            SenderOrgNumber = dto.SenderOrgNumber ?? string.Empty,
            ResourceId = dto.ResourceId,
            ResourceTitle = resourceTitles.GetValueOrDefault(dto.ResourceId) ?? GetResourceTitle(dto.ResourceId),
            RecipientType = dto.RecipientType,
            AltinnVersion = dto.AltinnVersion,
            MessageCount = dto.MessageCount,
            DatabaseStorageBytes = dto.DatabaseStorageBytes,
            AttachmentStorageBytes = dto.AttachmentStorageBytes,
            ShipmentId = dto.ShipmentId,
            ReminderShipmentId = dto.ReminderShipmentId
        }).ToList();
    }

    private async Task<string> GetResourceTitle(string resourceId, CancellationToken cancellationToken)
    {
        try
        {
            var resourceTitle = await resourceRegistryService.GetResourceTitle(resourceId, null, cancellationToken);
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

        // Monthly: "daily_summary_report_202609_A3_Production.parquet"
        var monthlyMatch = System.Text.RegularExpressions.Regex.Match(
            reportName,
            @"daily_summary_report_(\d{6})_");
        if (monthlyMatch.Success
            && DateTimeOffset.TryParseExact(
                monthlyMatch.Groups[1].Value + "01",
                "yyyyMMdd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal,
                out var monthlyResult))
        {
            return monthlyResult;
        }

        // Legacy: "20240127_143055_daily_summary_report_v2_production.parquet"
        var parts = reportName.Split('_');

        if (parts.Length < 2)
        {
            throw new FormatException($"Report name '{reportName}' does not contain expected datetime format");
        }

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

        throw new FormatException($"Unable to parse datetime from report name '{reportName}'. Expected monthly (yyyyMM) or legacy (yyyyMMdd_HHmmss) format");
    }

    private async Task<(string blobUrl, string fileHash, long fileSize)> GenerateAndUploadParquetFile(
        List<DailySummaryData> summaryData,
        int correspondenceCount,
        bool altinn2Included,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        var fileName = BuildMonthlyReportFileName(
            year,
            month,
            altinn2Included,
            hostEnvironment.EnvironmentName ?? "Unknown");

        logger.LogInformation(
            "Generating monthly daily summary parquet file {fileName} with {count} records for blob storage",
            fileName,
            summaryData.Count);

        var (parquetStream, fileHash, fileSize) = await GenerateParquetFileStream(summaryData, cancellationToken);

        var serviceOwnerCount = summaryData.Select(d => d.ServiceOwnerId).Distinct().Count();

        var (blobUrl, _, _) = await storageRepository.UploadReportFile(fileName, serviceOwnerCount, correspondenceCount, parquetStream, cancellationToken);

        logger.LogInformation("Successfully generated and uploaded monthly daily summary parquet file to blob storage: {blobUrl}", blobUrl);

        return (blobUrl, fileHash, fileSize);
    }

    private async Task<(Stream parquetStream, string fileHash, long fileSize)> GenerateParquetFileStream(
        List<DailySummaryData> summaryData,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Generating daily summary parquet file with {count} records", summaryData.Count);

        var parquetData = summaryData.Select(d => new ParquetDailySummaryData
        {
            CorrespondenceId = d.CorrespondenceId.ToString(),
            Date = d.Date.ToString("yyyy-MM-dd"),
            Year = d.Year,
            Month = d.Month,
            Day = d.Day,
            ServiceOwnerId = d.ServiceOwnerId,
            ServiceOwnerName = d.ServiceOwnerName,
            MessageSender = d.MessageSender,
            SenderOrgNumber = d.SenderOrgNumber,
            ResourceId = d.ResourceId,
            ResourceTitle = d.ResourceTitle,
            RecipientType = d.RecipientType.ToString(),
            AltinnVersion = d.AltinnVersion.ToString(),
            MessageCount = d.MessageCount,
            DatabaseStorageBytes = d.DatabaseStorageBytes,
            AttachmentStorageBytes = d.AttachmentStorageBytes,
            ShipmentId = d.ShipmentId?.ToString(),
            ReminderShipmentId = d.ReminderShipmentId?.ToString()
        }).ToList();

        var memoryStream = new MemoryStream();
        
        await ParquetSerializer.SerializeAsync(parquetData, memoryStream, cancellationToken: cancellationToken);
        memoryStream.Position = 0;

        using var md5 = MD5.Create();
        var hash = Convert.ToBase64String(md5.ComputeHash(memoryStream.ToArray()));
        memoryStream.Position = 0;

        logger.LogInformation("Successfully generated daily summary parquet file stream");

        return (memoryStream, hash, memoryStream.Length);
    }

    public async Task<OneOf<GenerateAndDownloadDailySummaryReportResponse, Error>> DownloadReportFile(
        GenerateDailySummaryReportRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Altinn2Included)
        {
            logger.LogWarning("Download of daily summary report with Altinn2Included=true is not supported. Returning error.");
            return StatisticsErrors.Altinn2NotSupported;
        }

        int year;
        int month;
        try
        {
            (year, month) = ResolveReportMonth(request);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid report month in download request");
            return StatisticsErrors.InvalidReportMonth;
        }

        var fileName = BuildMonthlyReportFileName(
            year,
            month,
            request.Altinn2Included,
            hostEnvironment.EnvironmentName ?? "Unknown");

        logger.LogInformation(
            "Starting monthly daily summary report download for {Year}-{Month:D2} (file {FileName})",
            year,
            month,
            fileName);

        try
        {
            var reportFile = await storageRepository.DownloadReportFile(fileName, cancellationToken);

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
                Altinn2Included = false
            };

            logger.LogInformation(
                "Successfully downloaded monthly daily summary report {FileName} with {serviceOwnerCount} service owners and {totalCount} correspondences",
                reportFile.FileName,
                response.ServiceOwnerCount,
                response.TotalCorrespondenceCount);

            return response;
        }
        catch (FileNotFoundException ex)
        {
            logger.LogWarning(ex, "Monthly daily summary report {FileName} was not found", fileName);
            return StatisticsErrors.ReportNotFound;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to download monthly daily summary report {FileName}", fileName);
            return StatisticsErrors.ReportGenerationFailed;
        }
    }

    public async Task<OneOf<GenerateAndDownloadDailySummaryReportResponse, Error>> ProcessAndDownload(
        GenerateDailySummaryReportRequest request,
        CancellationToken cancellationToken)
    {
        int year;
        int month;
        try
        {
            (year, month) = ResolveReportMonth(request);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid report month in generate-and-download request");
            return StatisticsErrors.InvalidReportMonth;
        }

        var (fromInclusive, toExclusive) = GetUtcMonthRange(year, month);

        logger.LogInformation(
            "Starting monthly daily summary report generation and download for {Year}-{Month:D2} with Altinn2Included={altinn2Included}",
            year,
            month,
            request.Altinn2Included);

        try
        {
            var summaryDataDto = await correspondenceRepository.GetDailySummaryData(
                request.Altinn2Included,
                fromInclusive,
                toExclusive,
                cancellationToken);
            
            if (!summaryDataDto.Any())
            {
                logger.LogWarning("No correspondences found for report generation for {Year}-{Month:D2}", year, month);
                return StatisticsErrors.NoCorrespondencesFound;
            }

            logger.LogInformation("Found {count} correspondence summary records for {Year}-{Month:D2}", summaryDataDto.Count, year, month);

            var summaryData = await MapToDailySummaryData(summaryDataDto, cancellationToken);

            var (parquetStream, fileHash, fileSize) = await GenerateParquetFileStream(summaryData, cancellationToken);

            var fileName = BuildMonthlyReportFileName(
                year,
                month,
                request.Altinn2Included,
                hostEnvironment.EnvironmentName ?? "Unknown");

            var response = new GenerateAndDownloadDailySummaryReportResponse
            {
                FileStream = parquetStream,
                FileName = fileName,
                FileHash = fileHash,
                FileSizeBytes = fileSize,
                ServiceOwnerCount = summaryData.Select(d => d.ServiceOwnerId).Distinct().Count(),
                TotalCorrespondenceCount = summaryData.Count,
                GeneratedAt = DateTimeOffset.UtcNow,
                Environment = hostEnvironment.EnvironmentName,
                Altinn2Included = request.Altinn2Included
            };

            logger.LogInformation("Successfully generated monthly daily summary report for download with {serviceOwnerCount} service owners and {totalCount} correspondences", 
                response.ServiceOwnerCount, response.TotalCorrespondenceCount);

            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate monthly daily summary report for download");
            return StatisticsErrors.ReportGenerationFailed;
        }
    }
}
