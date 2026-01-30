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
        var jobId = backgroundJobClient.Enqueue(() =>
            ExecuteInBackground(request.BatchSize, request.OlderThanDays, CancellationToken.None));

        return Task.FromResult<OneOf<EnqueueMissingNotificationSentChecksResponse, Error>>(new EnqueueMissingNotificationSentChecksResponse
        {
            JobId = jobId,
            Message = "Repair enqueue job has been enqueued"
        });
    }

    [AutomaticRetry(Attempts = 0)]
    [DisableConcurrentExecution(timeoutInSeconds: 1800)]
    public async Task ExecuteInBackground(
        int batchSize,
        int olderThanDays,
        CancellationToken cancellationToken)
    {
        var olderThan = DateTimeOffset.UtcNow.AddDays(-olderThanDays);
        logger.LogInformation(
            "Starting notification sent repair enqueue (batchSize={batchSize}, olderThan={olderThan})",
            batchSize,
            olderThan);

        Guid? afterNotificationId = null;

        var totalCandidates = 0;
        var totalEnqueued = 0;
        var totalSkippedHasActivity = 0;
        var batches = 0;

        while (true)
        {
            var candidates = await correspondenceNotificationRepository.GetAltinn3NotificationDeliveryRepairCandidates(
                olderThan,
                afterNotificationId,
                batchSize,
                cancellationToken);

            if (candidates.Count == 0)
            {
                break;
            }

            batches++;
            totalCandidates += candidates.Count;

            foreach (var candidate in candidates)
            {
                var didEnqueue = await EnqueueDeliveryCheckIfMissingActivity(candidate, cancellationToken);
                if (didEnqueue)
                {
                    totalEnqueued++;
                }
                else
                {
                    totalSkippedHasActivity++;
                }
            }

            var last = candidates[^1];
            afterNotificationId = last.NotificationId;
        }

        logger.LogInformation(
            "Repair enqueue done. Batches={batches}, Candidates={candidates}, Enqueued={enqueued}, SkippedHasActivity={skippedHasActivity}",
            batches,
            totalCandidates,
            totalEnqueued,
            totalSkippedHasActivity);
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
