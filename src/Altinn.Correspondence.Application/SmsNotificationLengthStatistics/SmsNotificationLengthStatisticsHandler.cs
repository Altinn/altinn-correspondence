using System.Security.Claims;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Hangfire;
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

    public Task<OneOf<SmsNotificationLengthStatisticsResponse, Error>> Process(
        SmsNotificationLengthStatisticsRequest request,
        ClaimsPrincipal? user,
        CancellationToken cancellationToken)
    {
        var from = request.From ?? DefaultFrom;
        var to = request.To ?? DefaultTo;
        var batchSize = request.BatchSize is > 0 ? request.BatchSize.Value : DefaultBatchSize;

        logger.LogInformation(
            "Enqueuing sms-notification-length-statistics job for range {From} to {To} with batchSize {BatchSize}",
            from, to, batchSize);

        var jobId = backgroundJobClient.Enqueue<SmsNotificationLengthStatisticsHandler>(
            handler => handler.Execute(from, to, batchSize, CancellationToken.None));

        return Task.FromResult<OneOf<SmsNotificationLengthStatisticsResponse, Error>>(
            new SmsNotificationLengthStatisticsResponse
            {
                JobId = jobId,
                From = from,
                To = to,
                BatchSize = batchSize
            });
    }

    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(timeoutInSeconds: 86400)]
    public async Task Execute(
        DateTimeOffset from,
        DateTimeOffset to,
        int batchSize,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Starting sms-notification-length-statistics: range {From}..{To} batchSize {BatchSize}",
            from, to, batchSize);

        var recipientNameCache = new Dictionary<string, string?>(StringComparer.Ordinal);
        var resourceTitleCache = new Dictionary<string, string?>(StringComparer.Ordinal);
        var totals = new SmsLengthStats();
        var perCategoryTotals = new Dictionary<RecipientCategory, SmsLengthStats>
        {
            [RecipientCategory.Organization] = new SmsLengthStats(),
            [RecipientCategory.Person] = new SmsLengthStats()
        };

        Guid? cursor = null;
        var batchIndex = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            var batch = await correspondenceRepository.GetCorrespondencesCreatedInRange(
                from, to, cursor, batchSize, cancellationToken);

            if (batch.Count == 0)
            {
                break;
            }

            batchIndex++;
            var batchStats = new SmsLengthStats();

            foreach (var c in batch)
            {
                try
                {
                    var category = RecipientCategoryClassifier.Classify(c.RecipientType, c.Recipient);
                    var categoryStats = perCategoryTotals[category];
                    var recipientNumber = c.Recipient.WithoutPrefix();

                    if (!recipientNameCache.TryGetValue(c.Recipient, out var recipientName))
                    {
                        try
                        {
                            recipientName = await altinnRegisterService.LookUpName(recipientNumber, cancellationToken);
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
                        recipientNameCache[c.Recipient] = recipientName;
                    }

                    if (!resourceTitleCache.TryGetValue(c.ResourceId, out var resourceName))
                    {
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
                        resourceTitleCache[c.ResourceId] = resourceName;
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

            cursor = batch[^1].Id;

            logger.LogInformation(
                "SMS length batch {BatchIndex} processed {ProcessedInBatch} correspondences. TotalCorrespondencesCheckedSoFar={TotalCorrespondencesCheckedSoFar}. Batch stats: {BatchStats}",
                batchIndex, batch.Count, totals.Count, batchStats.ToLogString());

            if (batch.Count < batchSize)
            {
                break;
            }
        }

        logger.LogInformation(
            "Completed sms-notification-length-statistics for range {From}..{To}. TotalCorrespondencesChecked={TotalCorrespondencesChecked}. Totals: {TotalStats}",
            from, to, totals.Count, totals.ToLogString());

        foreach (var category in new[] { RecipientCategory.Organization, RecipientCategory.Person })
        {
            var categoryStats = perCategoryTotals[category];
            logger.LogInformation(
                "sms-notification-length-statistics by RecipientCategory={RecipientCategory} CorrespondencesChecked={CorrespondencesChecked}. Stats: {CategoryStats}",
                category, categoryStats.Count, categoryStats.ToLogString());
        }
    }
}
