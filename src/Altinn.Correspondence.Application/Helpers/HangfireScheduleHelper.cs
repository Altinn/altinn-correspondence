using Altinn.Correspondence.Application.PublishCorrespondence;
using Altinn.Correspondence.Common.Caching;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Application.Helpers
{
    public class HangfireScheduleHelper(IBackgroundJobClient backgroundJobClient,
                                        IHybridCacheWrapper hybridCacheWrapper,
                                        ICorrespondenceRepository correspondenceRepository,
                                        ILogger<HangfireScheduleHelper> logger)
    {

        public async Task SchedulePublishAfterDialogCreated(CorrespondenceEntity correspondence, CancellationToken cancellationToken)
        {
            var dialogJobId = await hybridCacheWrapper.GetAsync<string?>("dialogJobId_" + correspondence.Id);
            if (dialogJobId is null)
            {
                logger.LogError("Could not find dialogJobId for correspondence {correspondenceId} in cache. More than 24 hours delayed?", correspondence.Id);
                await SchedulePublishAtPublishTime(correspondence.Id, cancellationToken);
            }
            else
            {
                backgroundJobClient.ContinueJobWith<HangfireScheduleHelper>(dialogJobId, (helper) => helper.SchedulePublishAtPublishTime(correspondence.Id, cancellationToken));
            }
        }

        public async Task SchedulePublishAtPublishTime(Guid correspondenceId, CancellationToken cancellationToken)
        {
            var correspondence = await correspondenceRepository.GetCorrespondenceById(correspondenceId, true, false, cancellationToken);
            if (correspondence is null)
            {
                throw new Exception($"Correspondence with id {correspondenceId} not found when scheduling publish");
            }
            backgroundJobClient.Schedule<PublishCorrespondenceHandler>((handler) => handler.Process(correspondence.Id, null, cancellationToken), GetActualPublishTime(correspondence.RequestedPublishTime));
        }

        private static DateTimeOffset GetActualPublishTime(DateTimeOffset publishTime) => publishTime < DateTimeOffset.UtcNow ? DateTimeOffset.UtcNow : publishTime; // If in past, do now
    }
}
