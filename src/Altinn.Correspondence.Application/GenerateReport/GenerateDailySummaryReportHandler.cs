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
using Parquet;
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
    /// Defaults to the preceding Europe/Oslo day; optional Year/Month (and Day) allow backfill of
    /// completed periods. Today and future Europe/Oslo days are rejected.
    /// Use download endpoints to fetch the parquet file after the job completes.
    /// </summary>
    public Task<OneOf<EnqueueDailySummaryReportResponse, Error>> Process(
        GenerateDailySummaryReportRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Altinn2Included)
        {
            logger.LogWarning("Enqueue of daily summary report with Altinn2Included=true is not supported. Returning error.");
            return Task.FromResult<OneOf<EnqueueDailySummaryReportResponse, Error>>(StatisticsErrors.Altinn2NotSupported);
        }

        int year;
        int month;
        int? day;
        try
        {
            (year, month, day) = ResolveReportPeriod(request);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid report period in generate request");
            return Task.FromResult<OneOf<EnqueueDailySummaryReportResponse, Error>>(MapPeriodArgumentException(ex));
        }

        string jobId;
        string message;
        if (day is int dayValue)
        {
            logger.LogInformation(
                "Enqueueing daily summary report generation for {Year}-{Month:D2}-{Day:D2} with Altinn2Included={altinn2Included}",
                year,
                month,
                dayValue,
                request.Altinn2Included);

            jobId = backgroundJobClient.Enqueue(() =>
                ExecuteDayInBackground(request.Altinn2Included, year, month, dayValue, CancellationToken.None));
            message = $"Daily summary report generation for {year}-{month:D2}-{dayValue:D2} has been enqueued. Use the download endpoint when the job has completed.";
        }
        else
        {
            logger.LogInformation(
                "Enqueueing monthly daily summary report generation for {Year}-{Month:D2} with Altinn2Included={altinn2Included}",
                year,
                month,
                request.Altinn2Included);

            jobId = backgroundJobClient.Enqueue(() =>
                ExecuteInBackground(request.Altinn2Included, year, month, CancellationToken.None));
            message = $"Monthly daily summary report generation for {year}-{month:D2} has been enqueued. Use the download endpoint when the job has completed.";
        }

        logger.LogInformation(
            "Daily summary report generation job {JobId} has been enqueued for {Year}-{Month:D2}{DaySuffix}",
            jobId,
            year,
            month,
            day is int d ? $"-{d:D2}" : string.Empty);

        return Task.FromResult<OneOf<EnqueueDailySummaryReportResponse, Error>>(new EnqueueDailySummaryReportResponse
        {
            JobId = jobId,
            Message = message,
            Altinn2Included = request.Altinn2Included,
            Year = year,
            Month = month,
            Day = day
        });
    }

    /// <summary>
    /// Regenerates the preceding Europe/Oslo day. Used by the daily Hangfire recurring job.
    /// </summary>
    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(timeoutInSeconds: 14400)]
    public Task ExecutePrecedingDayInBackground(bool altinn2Included, CancellationToken cancellationToken)
    {
        var (year, month, day) = ResolvePrecedingReportDay(DateTimeOffset.UtcNow);
        return ExecuteDayInBackground(altinn2Included, year, month, day, cancellationToken);
    }

    /// <summary>
    /// Performs report generation and upload for a single UTC month.
    /// Invoked by Hangfire when a monthly report is enqueued via the API.
    /// </summary>
    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(timeoutInSeconds: 14400)]
    public Task ExecuteInBackground(bool altinn2Included, int year, int month, CancellationToken cancellationToken)
        => GenerateAndUploadReport(altinn2Included, year, month, day: null, cancellationToken);

    /// <summary>
    /// Performs report generation and upload for a single UTC day.
    /// </summary>
    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(timeoutInSeconds: 14400)]
    public Task ExecuteDayInBackground(bool altinn2Included, int year, int month, int day, CancellationToken cancellationToken)
        => GenerateAndUploadReport(altinn2Included, year, month, day, cancellationToken);

    private async Task GenerateAndUploadReport(
        bool altinn2Included,
        int year,
        int month,
        int? day,
        CancellationToken cancellationToken)
    {
        var (fromInclusive, toExclusive) = day is int dayValue
            ? GetOsloDayRange(year, month, dayValue)
            : GetUtcMonthRange(year, month);
        var periodLabel = FormatPeriodLabel(year, month, day);

        logger.LogInformation(
            "Starting daily summary report generation for {Period} (range [{From}, {To})) Altinn2Included={altinn2Included}",
            periodLabel,
            fromInclusive,
            toExclusive,
            altinn2Included);

        try
        {
            var fileName = BuildReportFileName(
                year,
                month,
                day,
                altinn2Included,
                hostEnvironment.EnvironmentName ?? "Unknown");

            var (tempPath, _, _, serviceOwnerCount, correspondenceCount, rowCount) =
                await GenerateParquetToTempFile(altinn2Included, fromInclusive, toExclusive, periodLabel, cancellationToken);

            if (tempPath is null)
            {
                logger.LogWarning("No correspondences found for daily summary report {Period}", periodLabel);
                return;
            }

            try
            {
                await using var fileStream = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                var (blobUrl, _, _) = await storageRepository.UploadReportFile(
                    fileName,
                    serviceOwnerCount,
                    correspondenceCount,
                    fileStream,
                    cancellationToken);

                logger.LogInformation(
                    "Successfully generated and uploaded daily summary report for {Period} to blob storage: {blobUrl} ({RowCount} rows, {CorrespondenceCount} correspondences)",
                    periodLabel,
                    blobUrl,
                    rowCount,
                    correspondenceCount);
            }
            finally
            {
                TryDeleteTempFile(tempPath);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate daily summary report for {Period}", periodLabel);
            throw;
        }
    }

    private static readonly TimeZoneInfo OsloTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Oslo");

    /// <summary>
    /// Resolves year/month only (no day). Prefer <see cref="ResolveReportPeriod"/> for API defaults.
    /// Omitting year/month uses the current UTC month. Day must not be set.
    /// </summary>
    public static (int Year, int Month) ResolveReportMonth(GenerateDailySummaryReportRequest request)
    {
        if (request.Day is not null)
        {
            throw new ArgumentException("Day must not be set when resolving a monthly report period.");
        }

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

    /// <summary>
    /// Resolves the report period from the request.
    /// Omitting year/month/day uses the preceding Europe/Oslo calendar day.
    /// Year+Month yields a monthly report; Year+Month+Day yields a single-day report.
    /// Single-day reports for the current or future Europe/Oslo day are rejected.
    /// </summary>
    public static (int Year, int Month, int? Day) ResolveReportPeriod(GenerateDailySummaryReportRequest request)
    {
        if (request.Year is null && request.Month is null && request.Day is null)
        {
            var (year, month, day) = ResolvePrecedingReportDay(DateTimeOffset.UtcNow);
            return (year, month, day);
        }

        if (request.Year is null || request.Month is null)
        {
            throw new ArgumentException("Year and Month must both be provided when specifying a report period.");
        }

        if (request.Month is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(request.Month), request.Month, "Month must be between 1 and 12.");
        }

        if (request.Year < 2025)
        {
            throw new ArgumentOutOfRangeException(nameof(request.Year), request.Year, "Year must be 2025 or later.");
        }

        if (request.Day is null)
        {
            return (request.Year.Value, request.Month.Value, null);
        }

        var daysInMonth = DateTime.DaysInMonth(request.Year.Value, request.Month.Value);
        if (request.Day is < 1 || request.Day > daysInMonth)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.Day),
                request.Day,
                $"Day must be between 1 and {daysInMonth} for {request.Year.Value}-{request.Month.Value:D2}.");
        }

        var requestedDate = new DateOnly(request.Year.Value, request.Month.Value, request.Day.Value);
        var todayOslo = GetOsloCalendarDate(DateTimeOffset.UtcNow);
        if (requestedDate >= todayOslo)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request.Day),
                request.Day,
                "Reports cannot be generated for the current or future Europe/Oslo day because the day is not finished yet.");
        }

        return (request.Year.Value, request.Month.Value, request.Day.Value);
    }

    private static Error MapPeriodArgumentException(ArgumentException ex)
    {
        if (ex is ArgumentOutOfRangeException
            && ex.Message.Contains("current or future Europe/Oslo day", StringComparison.Ordinal))
        {
            return StatisticsErrors.ReportDayNotComplete;
        }

        return StatisticsErrors.InvalidReportMonth;
    }

    /// <summary>
    /// Resolves the preceding Europe/Oslo calendar day for the daily recurring report job.
    /// </summary>
    public static (int Year, int Month, int Day) ResolvePrecedingReportDay(DateTimeOffset now)
    {
        var precedingDay = GetOsloCalendarDate(now).AddDays(-1);
        return (precedingDay.Year, precedingDay.Month, precedingDay.Day);
    }

    public static DateOnly GetOsloCalendarDate(DateTimeOffset instant)
    {
        var osloDateTime = TimeZoneInfo.ConvertTime(instant, OsloTimeZone);
        return DateOnly.FromDateTime(osloDateTime.DateTime);
    }

    public static (DateTimeOffset FromInclusive, DateTimeOffset ToExclusive) GetUtcMonthRange(int year, int month)
    {
        var fromInclusive = new DateTimeOffset(year, month, 1, 0, 0, 0, TimeSpan.Zero);
        return (fromInclusive, fromInclusive.AddMonths(1));
    }

    /// <summary>
    /// Returns the half-open Created range [from, to) covering one Europe/Oslo calendar day,
    /// expressed as UTC instants (handles DST transitions).
    /// </summary>
    public static (DateTimeOffset FromInclusive, DateTimeOffset ToExclusive) GetOsloDayRange(int year, int month, int day)
    {
        var startLocal = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Unspecified);
        var endLocal = startLocal.AddDays(1);
        var fromUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, OsloTimeZone);
        var toUtc = TimeZoneInfo.ConvertTimeToUtc(endLocal, OsloTimeZone);
        return (new DateTimeOffset(fromUtc, TimeSpan.Zero), new DateTimeOffset(toUtc, TimeSpan.Zero));
    }

    public static string BuildMonthlyReportFileName(int year, int month, bool altinn2Included, string environmentName)
        => BuildReportFileName(year, month, day: null, altinn2Included, environmentName);

    public static string BuildDailyReportFileName(int year, int month, int day, bool altinn2Included, string environmentName)
        => BuildReportFileName(year, month, day, altinn2Included, environmentName);

    public static string BuildReportFileName(int year, int month, int? day, bool altinn2Included, string environmentName)
    {
        var altinnVersionIndicator = altinn2Included ? "A2A3" : "A3";
        var period = day is int dayValue
            ? $"{year:D4}{month:D2}{dayValue:D2}"
            : $"{year:D4}{month:D2}";
        return $"daily_summary_report_{period}_{altinnVersionIndicator}_{environmentName}.parquet";
    }

    private static string FormatPeriodLabel(int year, int month, int? day)
        => day is int dayValue ? $"{year}-{month:D2}-{dayValue:D2}" : $"{year}-{month:D2}";

    private async Task<(string? TempPath, string FileHash, long FileSize, int ServiceOwnerCount, int CorrespondenceCount, int RowCount)> GenerateParquetToTempFile(
        bool altinn2Included,
        DateTimeOffset fromInclusive,
        DateTimeOffset toExclusive,
        string periodLabel,
        CancellationToken cancellationToken)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"daily_summary_{periodLabel.Replace('-', '_')}_{Guid.NewGuid():N}.parquet");
        var resourceTitleCache = new Dictionary<string, string>(StringComparer.Ordinal);
        var serviceOwnerIds = new HashSet<string>(StringComparer.Ordinal);
        var correspondenceCount = 0;
        var rowCount = 0;
        var wroteAny = false;

        try
        {
            await using (var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
            {
                await foreach (var batch in correspondenceRepository.StreamDailySummaryBatches(
                    altinn2Included,
                    fromInclusive,
                    toExclusive,
                    cancellationToken))
                {
                    if (batch.Count == 0)
                    {
                        continue;
                    }

                    await EnsureResourceTitlesCached(batch, resourceTitleCache, cancellationToken);

                    var parquetBatch = MapBatchToParquet(batch, resourceTitleCache);
                    correspondenceCount += batch.Select(d => d.CorrespondenceId).Distinct().Count();
                    foreach (var serviceOwnerId in batch.Select(d => d.ServiceOwnerId))
                    {
                        serviceOwnerIds.Add(serviceOwnerId);
                    }

                    await ParquetSerializer.SerializeAsync(
                        parquetBatch,
                        fileStream,
                        new ParquetOptions { Append = wroteAny },
                        cancellationToken: cancellationToken);
                    wroteAny = true;
                    rowCount += parquetBatch.Count;

                    logger.LogInformation(
                        "Appended {BatchRows} parquet rows for {Period} (total rows {TotalRows}, correspondences {CorrespondenceCount})",
                        parquetBatch.Count,
                        periodLabel,
                        rowCount,
                        correspondenceCount);
                }
            }

            if (!wroteAny)
            {
                TryDeleteTempFile(tempPath);
                return (null, string.Empty, 0, 0, 0, 0);
            }

            await using (var readStream = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using var md5 = MD5.Create();
                var hash = Convert.ToBase64String(await md5.ComputeHashAsync(readStream, cancellationToken));
                return (tempPath, hash, readStream.Length, serviceOwnerIds.Count, correspondenceCount, rowCount);
            }
        }
        catch
        {
            TryDeleteTempFile(tempPath);
            throw;
        }
    }

    private async Task EnsureResourceTitlesCached(
        IReadOnlyList<DailySummaryDataDto> batch,
        Dictionary<string, string> resourceTitleCache,
        CancellationToken cancellationToken)
    {
        var missingResourceIds = batch
            .Select(d => d.ResourceId)
            .Where(id => !string.IsNullOrEmpty(id) && id != "unknown" && !resourceTitleCache.ContainsKey(id))
            .Distinct()
            .ToList();

        if (missingResourceIds.Count == 0)
        {
            return;
        }

        var tasks = missingResourceIds.ToDictionary(
            resourceId => resourceId,
            resourceId => GetResourceTitle(resourceId, cancellationToken));
        await Task.WhenAll(tasks.Values);

        foreach (var (resourceId, task) in tasks)
        {
            resourceTitleCache[resourceId] = task.Result;
        }
    }

    private List<ParquetDailySummaryData> MapBatchToParquet(
        IReadOnlyList<DailySummaryDataDto> batch,
        Dictionary<string, string> resourceTitleCache)
    {
        return batch.Select(dto => new ParquetDailySummaryData
        {
            CorrespondenceId = dto.CorrespondenceId.ToString(),
            Date = dto.Date.ToString("yyyy-MM-dd"),
            Year = dto.Year,
            Month = dto.Month,
            Day = dto.Day,
            ServiceOwnerId = dto.ServiceOwnerId,
            ServiceOwnerName = dto.ServiceOwnerName ?? GetServiceOwnerName(dto.ServiceOwnerId),
            MessageSender = dto.MessageSender,
            SenderOrgNumber = dto.SenderOrgNumber ?? string.Empty,
            ResourceId = dto.ResourceId,
            ResourceTitle = resourceTitleCache.GetValueOrDefault(dto.ResourceId) ?? GetResourceTitle(dto.ResourceId),
            RecipientType = dto.RecipientType.ToString(),
            AltinnVersion = dto.AltinnVersion.ToString(),
            DatabaseStorageBytes = dto.DatabaseStorageBytes,
            AttachmentStorageBytes = dto.AttachmentStorageBytes,
            ShipmentId = dto.ShipmentId?.ToString(),
            IsReminder = dto.IsReminder,
            NotificationSent = dto.NotificationSent?.UtcDateTime.ToString("O")
        }).ToList();
    }

    private static void TryDeleteTempFile(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup of temp parquet files.
        }
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

        // Daily: "daily_summary_report_20260915_A3_Production.parquet"
        var dailyMatch = System.Text.RegularExpressions.Regex.Match(
            reportName,
            @"daily_summary_report_(\d{8})_");
        if (dailyMatch.Success
            && DateTimeOffset.TryParseExact(
                dailyMatch.Groups[1].Value,
                "yyyyMMdd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal,
                out var dailyResult))
        {
            return dailyResult;
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

        throw new FormatException($"Unable to parse datetime from report name '{reportName}'. Expected daily (yyyyMMdd), monthly (yyyyMM), or legacy (yyyyMMdd_HHmmss) format");
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
        int? day;
        try
        {
            (year, month, day) = ResolveReportPeriod(request);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid report period in download request");
            return MapPeriodArgumentException(ex);
        }

        var fileName = BuildReportFileName(
            year,
            month,
            day,
            request.Altinn2Included,
            hostEnvironment.EnvironmentName ?? "Unknown");
        var periodLabel = FormatPeriodLabel(year, month, day);

        logger.LogInformation(
            "Starting daily summary report download for {Period} (file {FileName})",
            periodLabel,
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
                "Successfully downloaded daily summary report {FileName} with {serviceOwnerCount} service owners and {totalCount} correspondences",
                reportFile.FileName,
                response.ServiceOwnerCount,
                response.TotalCorrespondenceCount);

            return response;
        }
        catch (FileNotFoundException) when (day is int)
        {
            // Single-day reports are small enough to generate inline when missing.
            logger.LogInformation(
                "Daily summary report {FileName} was not found; generating for {Period} in the HTTP request",
                fileName,
                periodLabel);
            return await GenerateUploadAndReturnReport(
                request.Altinn2Included,
                year,
                month,
                day,
                cancellationToken);
        }
        catch (FileNotFoundException ex)
        {
            logger.LogWarning(ex, "Daily summary report {FileName} was not found", fileName);
            return StatisticsErrors.ReportNotFound;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to download daily summary report {FileName}", fileName);
            return StatisticsErrors.ReportGenerationFailed;
        }
    }

    public async Task<OneOf<GenerateAndDownloadDailySummaryReportResponse, Error>> ProcessAndDownload(
        GenerateDailySummaryReportRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Altinn2Included)
        {
            logger.LogWarning("Generate-and-download of daily summary report with Altinn2Included=true is not supported. Returning error.");
            return StatisticsErrors.Altinn2NotSupported;
        }

        int year;
        int month;
        int? day;
        try
        {
            (year, month, day) = ResolveReportPeriod(request);
        }
        catch (ArgumentException ex)
        {
            logger.LogWarning(ex, "Invalid report period in generate-and-download request");
            return MapPeriodArgumentException(ex);
        }

        logger.LogInformation(
            "Starting daily summary report generation and download for {Period} with Altinn2Included={altinn2Included}",
            FormatPeriodLabel(year, month, day),
            request.Altinn2Included);

        return await GenerateUploadAndReturnReport(
            request.Altinn2Included,
            year,
            month,
            day,
            cancellationToken);
    }

    private async Task<OneOf<GenerateAndDownloadDailySummaryReportResponse, Error>> GenerateUploadAndReturnReport(
        bool altinn2Included,
        int year,
        int month,
        int? day,
        CancellationToken cancellationToken)
    {
        var (fromInclusive, toExclusive) = day is int dayValue
            ? GetOsloDayRange(year, month, dayValue)
            : GetUtcMonthRange(year, month);
        var periodLabel = FormatPeriodLabel(year, month, day);
        var fileName = BuildReportFileName(
            year,
            month,
            day,
            altinn2Included,
            hostEnvironment.EnvironmentName ?? "Unknown");

        try
        {
            var (tempPath, fileHash, fileSize, serviceOwnerCount, correspondenceCount, _) =
                await GenerateParquetToTempFile(altinn2Included, fromInclusive, toExclusive, periodLabel, cancellationToken);

            if (tempPath is null)
            {
                logger.LogWarning("No correspondences found for report generation for {Period}", periodLabel);
                return StatisticsErrors.NoCorrespondencesFound;
            }

            try
            {
                await using (var uploadStream = new FileStream(tempPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    await storageRepository.UploadReportFile(
                        fileName,
                        serviceOwnerCount,
                        correspondenceCount,
                        uploadStream,
                        cancellationToken);
                }

                // DeleteOnClose so the temp parquet is removed after the response stream is disposed.
                var responseStream = new FileStream(
                    tempPath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    4096,
                    FileOptions.DeleteOnClose);

                var response = new GenerateAndDownloadDailySummaryReportResponse
                {
                    FileStream = responseStream,
                    FileName = fileName,
                    FileHash = fileHash,
                    FileSizeBytes = fileSize,
                    ServiceOwnerCount = serviceOwnerCount,
                    TotalCorrespondenceCount = correspondenceCount,
                    GeneratedAt = DateTimeOffset.UtcNow,
                    Environment = hostEnvironment.EnvironmentName,
                    Altinn2Included = altinn2Included
                };

                logger.LogInformation(
                    "Successfully generated daily summary report for {Period} with {serviceOwnerCount} service owners and {totalCount} correspondences",
                    periodLabel,
                    response.ServiceOwnerCount,
                    response.TotalCorrespondenceCount);

                return response;
            }
            catch
            {
                TryDeleteTempFile(tempPath);
                throw;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate daily summary report for {Period}", periodLabel);
            return StatisticsErrors.ReportGenerationFailed;
        }
    }
}
