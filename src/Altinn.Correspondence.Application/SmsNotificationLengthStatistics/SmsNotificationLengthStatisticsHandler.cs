using System.Security.Claims;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Extensions;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Hangfire;
using Hangfire.Server;
using Microsoft.Extensions.Logging;
using OneOf;

namespace Altinn.Correspondence.Application.SmsNotificationLengthStatistics;

public class SmsNotificationLengthStatisticsHandler(
    ICorrespondenceRepository correspondenceRepository,
    IAltinnRegisterService altinnRegisterService,
    IResourceRegistryService resourceRegistryService,
    IBackgroundJobClient backgroundJobClient,
    ILogger<SmsNotificationLengthStatisticsHandler> logger) : IHandler<SmsNotificationLengthStatisticsRequest, SmsNotificationLengthStatisticsResponse>
{
    private static readonly DateTimeOffset DefaultFrom = new(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset DefaultTo = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private const int DefaultBatchSize = 1000;
    private const int DefaultMaxBatches = 10000;

    public Task<OneOf<SmsNotificationLengthStatisticsResponse, Error>> Process(
        SmsNotificationLengthStatisticsRequest request,
        ClaimsPrincipal? user,
        CancellationToken cancellationToken)
    {
        var from = request.From ?? DefaultFrom;
        var to = request.To ?? DefaultTo;
        var batchSize = request.BatchSize is > 0 ? request.BatchSize.Value : DefaultBatchSize;
        var maxBatches = request.MaxBatches is > 0 ? request.MaxBatches.Value : DefaultMaxBatches;

        logger.LogInformation(
            "Enqueuing sms-notification-length-statistics job for range {From} to {To} with batchSize {BatchSize} maxBatches {MaxBatches}",
            from, to, batchSize, maxBatches);

        var jobId = backgroundJobClient.Enqueue<SmsNotificationLengthStatisticsHandler>(
            handler => handler.ExecuteBatch(
                from, to, batchSize, maxBatches,
                null, null,
                1,
                new SmsLengthStats(),
                new SmsLengthStats(),
                new SmsLengthStats(),
                null,
                CancellationToken.None));

        return Task.FromResult<OneOf<SmsNotificationLengthStatisticsResponse, Error>>(
            new SmsNotificationLengthStatisticsResponse
            {
                JobId = jobId,
                From = from,
                To = to,
                BatchSize = batchSize
            });
    }

    /// <summary>
    /// Processes a single batch of correspondences and then enqueues the next batch as its own
    /// Hangfire job, carrying the (Created, Id) keyset cursor and the accumulated statistics
    /// forward as job arguments. Keeping each job to one batch avoids the hangfire job invisibility
    /// timeout.
    /// </summary>
    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(timeoutInSeconds: 1800)]
    public async Task ExecuteBatch(
        DateTimeOffset from,
        DateTimeOffset to,
        int batchSize,
        int maxBatches,
        DateTimeOffset? cursorCreated,
        Guid? cursorId,
        int batchNumber,
        SmsLengthStats totals,
        SmsLengthStats organizationTotals,
        SmsLengthStats personTotals,
        PerformContext? performContext,
        CancellationToken cancellationToken)
    {
        var jobId = performContext?.BackgroundJob?.Id;

        logger.LogInformation(
            "Starting sms-notification-length-statistics batch {BatchNumber} (JobId={JobId}): range {From}..{To} batchSize {BatchSize} cursorCreated={CursorCreated} cursorId={CursorId}",
            batchNumber, jobId, from, to, batchSize, cursorCreated, cursorId);

        var perCategoryTotals = new Dictionary<RecipientCategory, SmsLengthStats>
        {
            [RecipientCategory.Organization] = organizationTotals,
            [RecipientCategory.Person] = personTotals
        };

        var batch = await correspondenceRepository.GetCorrespondencesCreatedInRange(
            from, to, cursorCreated, cursorId, batchSize, cancellationToken);

        if (batch.Count == 0)
        {
            LogCompletion(from, to, batchNumber - 1, totals, perCategoryTotals, jobId);
            return;
        }

        var batchStats = new SmsLengthStats();

        foreach (var c in batch)
        {
            try
            {
                var category = RecipientCategoryClassifier.Classify(c.RecipientType, c.Recipient);
                var categoryStats = perCategoryTotals[category];
                var recipientNumber = c.Recipient.WithoutPrefix();

                string? recipientName = null;
                try
                {
                    recipientName = (await altinnRegisterService.LookUpPartyById(recipientNumber, cancellationToken))?.GetDisplayName();
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "LookUpName failed for correspondence {CorrespondenceId}", c.Id);
                    recipientName = null;
                    totals.NameLookupFailures++;
                    batchStats.NameLookupFailures++;
                    categoryStats.NameLookupFailures++;
                }

                string? resourceName = null;
                try
                {
                    resourceName = await resourceRegistryService.GetResourceTitle(c.ResourceId, "nb", cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "GetResourceTitle failed for resource {ResourceId}", c.ResourceId);
                    resourceName = null;
                    totals.ResourceLookupFailures++;
                    batchStats.ResourceLookupFailures++;
                    categoryStats.ResourceLookupFailures++;
                }

                var effectiveRecipientName = string.IsNullOrWhiteSpace(recipientName) ? recipientNumber : recipientName;
                var effectiveResourceName = string.IsNullOrWhiteSpace(resourceName) ? c.ResourceId : resourceName;
                var effectiveTitle = c.Content?.MessageTitle ?? string.Empty;

                var body = $"{effectiveRecipientName} ({recipientNumber}) har mottatt «{effectiveTitle}» i Altinn. For å lese kreves tilgang til {effectiveResourceName}.";
                var length = body.Length;

                totals.Record(length);
                batchStats.Record(length);
                categoryStats.Record(length);

                if (!string.IsNullOrWhiteSpace(recipientName))
                {
                    totals.RecordRecipientNameLength(recipientName.Length);
                    batchStats.RecordRecipientNameLength(recipientName.Length);
                    categoryStats.RecordRecipientNameLength(recipientName.Length);
                }
                if (!string.IsNullOrWhiteSpace(resourceName))
                {
                    totals.RecordResourceNameLength(resourceName.Length);
                    batchStats.RecordResourceNameLength(resourceName.Length);
                    categoryStats.RecordResourceNameLength(resourceName.Length);
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to compute SMS length for correspondence {CorrespondenceId}", c.Id);
                totals.ProcessingFailures++;
                batchStats.ProcessingFailures++;
            }
        }

        var last = batch[^1];

        logger.LogInformation(
            "SMS length batch {BatchIndex} (JobId={JobId}) processed {ProcessedInBatch} correspondences. TotalCorrespondencesCheckedSoFar={TotalCorrespondencesCheckedSoFar}. Batch stats: {BatchStats}",
            batchNumber, jobId, batch.Count, totals.Count, batchStats.ToLogString());

        if (batch.Count < batchSize)
        {
            LogCompletion(from, to, batchNumber, totals, perCategoryTotals, jobId);
            return;
        }

        if (batchNumber >= maxBatches)
        {
            logger.LogWarning(
                "sms-notification-length-statistics hit the safety limit of {MaxBatches} batches (JobId={JobId}) before completing. Stopping. Resume by re-running with From=cursorCreated. Resume cursor: cursorCreated={CursorCreated} cursorId={CursorId}. Totals so far: {TotalStats}",
                maxBatches, jobId, last.Created, last.Id, totals.ToLogString());
            return;
        }

        var nextJobId = backgroundJobClient.Enqueue<SmsNotificationLengthStatisticsHandler>(
            handler => handler.ExecuteBatch(
                from, to, batchSize, maxBatches,
                last.Created, last.Id,
                batchNumber + 1,
                totals,
                perCategoryTotals[RecipientCategory.Organization],
                perCategoryTotals[RecipientCategory.Person],
                null,
                CancellationToken.None));

        logger.LogInformation(
            "Scheduled next sms-notification-length-statistics batch {NextBatch} as Hangfire job {NextJobId} (cursorCreated={CursorCreated} cursorId={CursorId})",
            batchNumber + 1, nextJobId, last.Created, last.Id);
    }

    private void LogCompletion(
        DateTimeOffset from,
        DateTimeOffset to,
        int batchesProcessed,
        SmsLengthStats totals,
        Dictionary<RecipientCategory, SmsLengthStats> perCategoryTotals,
        string? jobId)
    {
        logger.LogInformation(
            "Completed sms-notification-length-statistics for range {From}..{To} (JobId={JobId}). Batches={Batches}. TotalCorrespondencesChecked={TotalCorrespondencesChecked}. Totals: {TotalStats}",
            from, to, jobId, batchesProcessed, totals.Count, totals.ToLogString());

        foreach (var category in new[] { RecipientCategory.Organization, RecipientCategory.Person })
        {
            var categoryStats = perCategoryTotals[category];
            logger.LogInformation(
                "sms-notification-length-statistics by RecipientCategory={RecipientCategory} CorrespondencesChecked={CorrespondencesChecked}. Stats: {CategoryStats}",
                category, categoryStats.Count, categoryStats.ToLogString());
        }
    }
}
