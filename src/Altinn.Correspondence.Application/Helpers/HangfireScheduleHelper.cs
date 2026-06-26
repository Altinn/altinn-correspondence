using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.PublishCorrespondence;
using Altinn.Correspondence.Common.Caching;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Altinn.Correspondence.Integrations.Hangfire;
using Altinn.Correspondence.Persistence;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Application.Helpers
{
    public class HangfireScheduleHelper(
        IBackgroundJobClient backgroundJobClient,
        IHybridCacheWrapper hybridCacheWrapper,
        ICorrespondenceRepository correspondenceRepository,
        IIdempotencyKeyRepository idempotencyKeyRepository,
        ApplicationDbContext dbContext,
        ILogger<HangfireScheduleHelper> logger)
    {

        public void SchedulePublishAfterDialogCreated(Guid correspondenceId, string dialogJobId, CancellationToken cancellationToken)
        {
            backgroundJobClient.ContinueJobWith<HangfireScheduleHelper>(dialogJobId, HangfireQueues.LiveMigration, (helper) => helper.SchedulePublishAtPublishTime(correspondenceId, cancellationToken));
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
                logger.LogError("Could not find dialogJobId for correspondence {correspondenceId} in cache. More than 24 hours delayed?", correspondenceId);
                await SchedulePublishAtPublishTime(correspondenceId, cancellationToken);
            }
            else
            {
                #pragma warning disable CS4014 // Hangfire handles Task-returning job expressions by awaiting them during job execution
                backgroundJobClient.ContinueJobWith<HangfireScheduleHelper>(dialogJobId, (helper) => helper.SchedulePublishAtPublishTime(correspondenceId, cancellationToken), JobContinuationOptions.OnAnyFinishedState);
                #pragma warning restore CS4014
            }
        }

        public async Task SchedulePublishAfterTransmissionCreated(Guid correspondenceId, string transmissionJobId, CancellationToken cancellationToken)
        {
            if (transmissionJobId is null)
            {
                logger.LogError("Could not find transmissionJobId for correspondence {correspondenceId} in cache. More than 24 hours delayed?", correspondenceId);
                await SchedulePublishAtPublishTime(correspondenceId, cancellationToken);
            }
            else
            {
                #pragma warning disable CS4014 // Hangfire handles Task-returning job expressions by awaiting them during job execution
                backgroundJobClient.ContinueJobWith<HangfireScheduleHelper>(transmissionJobId, (helper) => helper.SchedulePublishAtPublishTime(correspondenceId, cancellationToken), JobContinuationOptions.OnAnyFinishedState);
                #pragma warning restore CS4014
            }
        }

        public async Task SchedulePublishAtPublishTime(Guid correspondenceId, CancellationToken cancellationToken)
        {
            var correspondence = await correspondenceRepository.GetCorrespondenceById(correspondenceId, true, false, false, cancellationToken);
            if (correspondence is null)
            {
                logger.LogError("Correspondence with id {CorrespondenceId} not found when scheduling publish", correspondenceId);
                return;
            }

            if (correspondence.StatusHasBeen(CorrespondenceStatus.Published) || correspondence.StatusHasBeen(CorrespondenceStatus.Failed))
            {
                logger.LogInformation("Skipping publish schedule for correspondence {CorrespondenceId} - already published or failed", correspondenceId);
                return;
            }

            var scheduleIdempotencyId = correspondenceId.CreateVersion5("SchedulePublishCorrespondence");
            var duplicateCheck = await DatabaseTransactionHelper.Idempotency.CheckAsync(
                idempotencyKeyRepository,
                scheduleIdempotencyId,
                () => Task.CompletedTask,
                cancellationToken);
            if (duplicateCheck.IsDuplicate)
            {
                logger.LogInformation("Publish already scheduled for correspondence {CorrespondenceId}; skipping", correspondenceId);
                return;
            }

            var publishTime = GetActualPublishTime(correspondence.RequestedPublishTime);
            await DatabaseTransactionHelper.ExecuteAsync(
                dbContext,
                async ct =>
                {
                    await DatabaseTransactionHelper.Idempotency.StageAsync(idempotencyKeyRepository, new IdempotencyKeyEntity
                    {
                        Id = scheduleIdempotencyId,
                        CorrespondenceId = correspondenceId,
                        AttachmentId = null,
                        PartyUrn = null,
                        StatusAction = null,
                        IdempotencyType = IdempotencyType.SchedulePublishCorrespondence
                    }, ct);

                    backgroundJobClient.Schedule<PublishCorrespondenceHandler>(
                        HangfireQueues.Default,
                        handler => handler.Process(correspondence.Id, null, ct),
                        publishTime);

                    logger.LogInformation(
                        "Scheduled publish for correspondence {CorrespondenceId} at {PublishTime}",
                        correspondenceId,
                        publishTime);

                    return Task.CompletedTask;
                },
                cancellationToken,
                DatabaseTransactionHelper.Idempotency.OnDuplicate(() =>
                {
                    logger.LogInformation("Publish already scheduled for correspondence {CorrespondenceId}; skipping", correspondenceId);
                    return Task.CompletedTask;
                }));
        }

        private static DateTimeOffset GetActualPublishTime(DateTimeOffset publishTime) => publishTime < DateTimeOffset.UtcNow ? DateTimeOffset.UtcNow : publishTime;


        public async Task CreateActivityAfterDialogCreated(Guid correspondenceId, NotificationOrderRequestV2 notification, DateTimeOffset operationTimestamp)
        {
            var dialogJobId = await hybridCacheWrapper.GetAsync<string?>($"dialogJobId_{correspondenceId}");
            if (dialogJobId is null)
            {
                logger.LogError("Could not find dialogJobId for correspondence {correspondenceId} in cache. More than 24 hours delayed?", correspondenceId);
                return;
            }
            backgroundJobClient.ContinueJobWith<IDialogportenService>(dialogJobId, (dialogPortenService) => dialogPortenService.CreateInformationActivity(correspondenceId, DialogportenActorType.ServiceOwner, DialogportenTextType.NotificationOrderCreated, operationTimestamp, notification.RequestedSendTime.ToString("yyyy-MM-dd HH:mm")), JobContinuationOptions.OnlyOnSucceededState);
        }
    }
}
