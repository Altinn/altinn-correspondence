using Altinn.Correspondence.Application.PublishCorrespondence;
using Altinn.Correspondence.Common.Caching;
using Altinn.Correspondence.Core.Models.Entities;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Application.Helpers
{
    public class HangfireScheduleHelper(IBackgroundJobClient backgroundJobClient, IHybridCacheWrapper hybridCacheWrapper, ILogger<HangfireScheduleHelper> logger)
    {

        public async Task PrepareForPublish(CorrespondenceEntity correspondence, CancellationToken cancellationToken)
        {
            var dialogJobId = await hybridCacheWrapper.GetAsync<string?>("dialogJobId_" + correspondence.Id);
            if (dialogJobId is null)
            {
                logger.LogError("Could not find dialogJobId for correspondence {correspondenceId} in cache. More than 24 hours delayed?", correspondence.Id);
                backgroundJobClient.Schedule<PublishCorrespondenceHandler>((handler) => handler.Process(correspondence.Id, null, cancellationToken), GetActualPublishTime(correspondence.RequestedPublishTime));
            }
            else
            {
                backgroundJobClient.Schedule<PublishCorrespondenceHandler>((handler) => handler.Process(correspondence.Id, null, cancellationToken), GetActualPublishTime(correspondence.RequestedPublishTime));
            }
        }

        private static DateTimeOffset GetActualPublishTime(DateTimeOffset publishTime) => publishTime < DateTimeOffset.UtcNow ? DateTimeOffset.UtcNow : publishTime; // If in past, do now
    }
}
