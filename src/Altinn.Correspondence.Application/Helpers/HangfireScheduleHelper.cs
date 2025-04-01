using Altinn.Correspondence.Application.PublishCorrespondence;
using Altinn.Correspondence.Common.Caching;
using Altinn.Correspondence.Core.Models.Entities;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Application.Helpers
{
    public class HangfireScheduleHelper(IBackgroundJobClient backgroundJobClient, IHybridCacheWrapper hybridCacheWrapper, ILogger<HangfireScheduleHelper> logger)
    {

        public void SchedulePublish(Guid correspondenceId, DateTimeOffset publishTime, CancellationToken cancellationToken)
        {
            backgroundJobClient.Schedule<PublishCorrespondenceHandler>((handler) => handler.Process(correspondenceId, null, cancellationToken), GetActualPublishTime(publishTime));
        }

        public async Task PrepareForPublish(CorrespondenceEntity correspondence, CancellationToken cancellationToken)
        {
            var dialogJobId = await hybridCacheWrapper.GetAsync<string?>("dialogJobId_" + correspondence.Id);
            if (dialogJobId is null)
            {
                logger.LogError("Could not find dialogJobId for correspondence {correspondenceId} in cache. More than 24 hours delayed?", correspondence.Id);
                backgroundJobClient.Schedule<PublishCorrespondenceHandler>((handler) => handler.Process(correspondence.Id, null, cancellationToken), GetActualPublishTime(publishTime));
            }
            else
            {
                backgroundJobClient.Schedule<PublishCorrespondenceHandler>((handler) => handler.Process(correspondence.Id, null, cancellationToken), GetActualPublishTime(publishTime));
            }
        }

        private static DateTimeOffset GetActualPublishTime(DateTimeOffset publishTime) => publishTime < DateTimeOffset.UtcNow ? DateTimeOffset.UtcNow : publishTime; // If in past, do now
    }
}
