using Altinn.Correspondence.Application.PublishCorrespondence;
using Altinn.Correspondence.Common.Caching;
using Altinn.Correspondence.Core.Extensions;
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
            // Correspondences initialized before publish was continued directly from the dialog job depend on this cached id
            var dialogJobId = await hybridCacheWrapper.GetAsync<string?>($"dialogJobId_{correspondenceId}", cancellationToken: cancellationToken);
            if (dialogJobId is not null)
            {
                SchedulePublishAfterJob(correspondenceId, dialogJobId, cancellationToken);
                return;
            }

            var correspondence = await correspondenceRepository.GetCorrespondenceById(correspondenceId, false, false, false, cancellationToken);
            var hasDialogportenDialog = correspondence?.ExternalReferences.Any(reference => reference.ReferenceType == ReferenceType.DialogportenDialogId) == true;
            if (!hasDialogportenDialog)
            {
                logger.LogInformation("Dialogporten dialog not yet created for correspondence {correspondenceId}. Publish continues from the dialog job when it finishes", correspondenceId);
                return;
            }
            if (correspondence?.IsTransmission() == true && !correspondence.HasCreatedTransmission())
            {
                logger.LogInformation("Dialogporten transmission not yet created for correspondence {correspondenceId}. Publish continues from the transmission job when it finishes", correspondenceId);
                return;
            }
            await SchedulePublishAtPublishTime(correspondenceId, cancellationToken);
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
