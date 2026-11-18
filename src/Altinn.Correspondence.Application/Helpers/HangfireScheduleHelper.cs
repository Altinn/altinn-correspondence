using Altinn.Correspondence.Application.PublishCorrespondence;
using Altinn.Correspondence.Common.Caching;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Integrations.Hangfire;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Application.Helpers
{
    public class HangfireScheduleHelper(IBackgroundJobClient backgroundJobClient,
        IHybridCacheWrapper hybridCacheWrapper,
        ICorrespondenceRepository correspondenceRepository,
        ILogger<HangfireScheduleHelper> logger)
    {

        public void SchedulePublishAfterJob(Guid correspondenceId, string parentJobId, CancellationToken cancellationToken)
        {
            #pragma warning disable CS4014 // Hangfire handles Task-returning job expressions by awaiting them during job execution
            backgroundJobClient.ContinueJobWith<HangfireScheduleHelper>(parentJobId, (helper) => helper.SchedulePublishAtPublishTime(correspondenceId, cancellationToken), JobContinuationOptions.OnAnyFinishedState);
            #pragma warning restore CS4014
        }

        public async Task SchedulePublishAfterDialogCreated(Guid correspondenceId, CancellationToken cancellationToken)
        {
            if (!await correspondenceRepository.AreAllAttachmentsPublished(correspondenceId, cancellationToken))
            {
                logger.LogInformation("Not all attachments published for correspondence {correspondenceId}, skipping publish scheduling", correspondenceId);
                return;
            }
            var dialogJobId = await hybridCacheWrapper.GetAsync<string?>($"dialogJobId_{correspondenceId}", cancellationToken: cancellationToken);
            if (dialogJobId is null)
            {
                logger.LogError("Could not find dialogJobId for correspondence {correspondenceId} in cache. Publish will wait until the Dialogporten dialog is created", correspondenceId);
                dialogJobId = backgroundJobClient.Enqueue<HangfireScheduleHelper>((helper) => helper.EnsureDialogportenDialogCreated(correspondenceId, cancellationToken));
            }
            SchedulePublishAfterJob(correspondenceId, dialogJobId, cancellationToken);
        }

        // Stands in for the dialog job when its id is missing from the cache. 12 attempts (~11h) outlast the default retries of the
        // notification job and the dialog job it may wait behind (~9.5h together).
        [AutomaticRetry(Attempts = 12, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
        public async Task EnsureDialogportenDialogCreated(Guid correspondenceId, CancellationToken cancellationToken)
        {
            var correspondence = await correspondenceRepository.GetCorrespondenceById(correspondenceId, false, false, false, cancellationToken);
            var hasDialogportenDialog = correspondence?.ExternalReferences.Any(reference => reference.ReferenceType == ReferenceType.DialogportenDialogId) == true;
            if (!hasDialogportenDialog)
            {
                throw new Exception($"Dialogporten dialog not yet created for correspondence {correspondenceId}");
            }
        }

        public async Task SchedulePublishAtPublishTime(Guid correspondenceId, CancellationToken cancellationToken)
        {
            var correspondence = await correspondenceRepository.GetCorrespondenceById(correspondenceId, false, false, false, cancellationToken);
            if (correspondence is null)
            {
                throw new Exception($"Correspondence with id {correspondenceId} not found when scheduling publish");
            }

            backgroundJobClient.Schedule<PublishCorrespondenceHandler>(HangfireQueues.Default, (handler) => handler.Process(correspondenceId, null, cancellationToken), GetActualPublishTime(correspondence.RequestedPublishTime));
        }

        private static DateTimeOffset GetActualPublishTime(DateTimeOffset publishTime) => publishTime < DateTimeOffset.UtcNow ? DateTimeOffset.UtcNow : publishTime; // If in past, do now
    }
}
