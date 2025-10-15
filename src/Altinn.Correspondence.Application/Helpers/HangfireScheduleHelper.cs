using Altinn.Correspondence.Application.InitializeCorrespondences;
using Altinn.Correspondence.Application.PublishCorrespondence;
using Altinn.Correspondence.Common.Caching;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Application.Helpers
{
    public class HangfireScheduleHelper(IBackgroundJobClient backgroundJobClient,
                                        IHybridCacheWrapper hybridCacheWrapper,
                                        ICorrespondenceRepository correspondenceRepository,
                                        ILogger<HangfireScheduleHelper> logger)
    {

        public async Task SchedulePublishAfterDialogCreated(Guid correspondenceId, NotificationRequest? notification, CancellationToken cancellationToken)
        {
            var dialogJobId = await hybridCacheWrapper.GetAsync<string?>($"dialogJobId_{correspondenceId}", cancellationToken: cancellationToken);
            if (dialogJobId is null)
            {
                logger.LogError("Could not find dialogJobId for correspondence {correspondenceId} in cache. More than 24 hours delayed?", correspondenceId);
                await SchedulePublishAtPublishTime(correspondenceId, notification, cancellationToken);
            }
            else
            {
                backgroundJobClient.ContinueJobWith<HangfireScheduleHelper>(dialogJobId, (helper) => helper.SchedulePublishAtPublishTime(correspondenceId, notification, cancellationToken));
            }
        }

        public async Task SchedulePublishAfterTransmissionCreated(Guid correspondenceId, NotificationRequest? notification, string transmissionJobId, CancellationToken cancellationToken)
        {
            if (transmissionJobId is null)
            {
                logger.LogError("Could not find transmissionJobId for correspondence {correspondenceId} in cache. More than 24 hours delayed?", correspondenceId);
                await SchedulePublishAtPublishTime(correspondenceId, notification, cancellationToken);
            }
            else
            {
                backgroundJobClient.ContinueJobWith<HangfireScheduleHelper>(transmissionJobId, (helper) => helper.SchedulePublishAtPublishTime(correspondenceId, notification, cancellationToken));
            }
        }

        public async Task SchedulePublishAtPublishTime(Guid correspondenceId, NotificationRequest? notification, CancellationToken cancellationToken)
        {
            var correspondence = await correspondenceRepository.GetCorrespondenceById(correspondenceId, true, false, false, cancellationToken);
            if (correspondence is null)
            {
                throw new Exception($"Correspondence with id {correspondenceId} not found when scheduling publish");
            }

            SchedulePublishAtPublishTime(correspondence, notification, cancellationToken);
        }

        public void SchedulePublishAtPublishTime(CorrespondenceEntity correspondence, NotificationRequest? notification, CancellationToken cancellationToken)
        {
            var request = new PublishCorrespondenceRequest
            {
                CorrespondenceId = correspondence.Id,
                NotificationRequest = notification
            };
            backgroundJobClient.Schedule<PublishCorrespondenceHandler>((handler) => handler.Process(request, null, cancellationToken), GetActualPublishTime(correspondence.RequestedPublishTime));
        }

        private static DateTimeOffset GetActualPublishTime(DateTimeOffset publishTime) => publishTime < DateTimeOffset.UtcNow ? DateTimeOffset.UtcNow : publishTime; // If in past, do now


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
