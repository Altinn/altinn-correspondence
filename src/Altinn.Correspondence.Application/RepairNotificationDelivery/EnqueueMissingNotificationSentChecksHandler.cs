using Altinn.Correspondence.Application.CheckNotificationDelivery;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Hangfire;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.RepairNotificationDelivery;

public sealed class EnqueueMissingNotificationSentChecksHandler(
    ICorrespondenceNotificationRepository correspondenceNotificationRepository,
    IDialogportenService dialogportenService,
    IBackgroundJobClient backgroundJobClient,
    ILogger<EnqueueMissingNotificationSentChecksHandler> logger)
    : IHandler<EnqueueMissingNotificationSentChecksRequest, EnqueueMissingNotificationSentChecksResponse>
{
    public Task<OneOf<EnqueueMissingNotificationSentChecksResponse, Error>> Process(
        EnqueueMissingNotificationSentChecksRequest request,
        ClaimsPrincipal? user,
        CancellationToken cancellationToken)
    {
        var olderThan = DateTimeOffset.UtcNow.AddDays(-request.OlderThanDays);
        var jobId = backgroundJobClient.Enqueue(() =>
            ExecuteBatchInBackground(
                request.BatchSize,
                olderThan,
                null,
                1,
                0,
                0,
                0,
                CancellationToken.None));

        return Task.FromResult<OneOf<EnqueueMissingNotificationSentChecksResponse, Error>>(new EnqueueMissingNotificationSentChecksResponse
        {
            JobId = jobId,
            Message = "Repair enqueue job has been enqueued"
        });
    }

    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(timeoutInSeconds: 1800)]
    public async Task ExecuteBatchInBackground(
        int batchSize,
        DateTimeOffset olderThan,
        Guid? afterNotificationId,
        int batchNumber,
        int totalCandidates,
        int totalEnqueued,
        int totalSkippedHasActivity,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Starting notification sent repair enqueue batch {BatchNumber} (batchSize={batchSize}, olderThan={olderThan}, afterNotificationId={afterNotificationId})",
            batchNumber,
            batchSize,
            olderThan,
            afterNotificationId);

        var candidates = await correspondenceNotificationRepository.GetAltinn3NotificationDeliveryRepairCandidates(
            olderThan,
            afterNotificationId,
            batchSize,
            cancellationToken);

        if (candidates.Count == 0)
        {
            logger.LogInformation(
                "Repair enqueue done. Batches={batches}, Candidates={candidates}, Enqueued={enqueued}, SkippedHasActivity={skippedHasActivity}",
                batchNumber - 1,
                totalCandidates,
                totalEnqueued,
                totalSkippedHasActivity);
            return;
        }

        var batchEnqueued = 0;
        var batchSkippedHasActivity = 0;

        foreach (var candidate in candidates)
        {
            var didEnqueue = await EnqueueDeliveryCheckIfMissingActivity(candidate, cancellationToken);
            if (didEnqueue)
            {
                batchEnqueued++;
            }
            else
            {
                batchSkippedHasActivity++;
            }
        }

        totalCandidates += candidates.Count;
        totalEnqueued += batchEnqueued;
        totalSkippedHasActivity += batchSkippedHasActivity;

        var last = candidates[^1];
        logger.LogInformation(
            "Batch {Batch} processed: Batch candidates={BatchCandidates}, Batch enqueued={BatchEnqueued}, Batch skipped={BatchSkippedHasActivity}, Total candidates={TotalCandidates}, Total enqueued={Enqueued}, Total skipped because already has activity={SkippedHasActivity}",
            batchNumber,
            candidates.Count,
            batchEnqueued,
            batchSkippedHasActivity,
            totalCandidates,
            totalEnqueued,
            totalSkippedHasActivity);

        if (candidates.Count < batchSize)
        {
            logger.LogInformation(
                "Repair enqueue done. Batches={batches}, Candidates={candidates}, Enqueued={enqueued}, SkippedHasActivity={skippedHasActivity}",
                batchNumber,
                totalCandidates,
                totalEnqueued,
                totalSkippedHasActivity);
            return;
        }

        var nextJobId = backgroundJobClient.Enqueue(() =>
            ExecuteBatchInBackground(
                batchSize,
                olderThan,
                last.NotificationId,
                batchNumber + 1,
                totalCandidates,
                totalEnqueued,
                totalSkippedHasActivity,
                CancellationToken.None));

        logger.LogInformation(
            "Scheduled next repair enqueue batch {NextBatch} as Hangfire job {JobId}",
            batchNumber + 1,
            nextJobId);
    }

    public async Task<bool> EnqueueDeliveryCheckIfMissingActivity(
        NotificationDeliveryRepairCandidate candidate,
        CancellationToken cancellationToken)
    {
        var textType = candidate.IsReminder
            ? DialogportenTextType.NotificationReminderSent
            : DialogportenTextType.NotificationSent;

        var hasActivity = await dialogportenService.HasInformationActivityByTextType(
            candidate.CorrespondenceId,
            textType,
            cancellationToken);

        if (hasActivity)
        {
            logger.LogInformation("Skipping notification {NotificationId} because it already has activity", candidate.NotificationId);
            return false;
        }

        backgroundJobClient.Enqueue<CheckNotificationDeliveryHandler>(h =>
            h.Process(candidate.NotificationId, CancellationToken.None, false));

        return true;
    }
}
