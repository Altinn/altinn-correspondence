using Altinn.Correspondence.Application.PublishCorrespondence;
using Hangfire;

namespace Altinn.Correspondence.Application.Helpers
{
    public class HangfireScheduleHelper(IBackgroundJobClient backgroundJobClient, PublishCorrespondenceHandler publishCorrespondenceHandler)
    {

        public void SchedulePublish(Guid correspondenceId, DateTimeOffset publishTime, CancellationToken cancellationToken)
        {
            var actualPublishTime = publishTime.AddSeconds(-30) < DateTimeOffset.UtcNow ? DateTimeOffset.UtcNow : publishTime; // If in past, do now
            backgroundJobClient.Schedule<PublishCorrespondenceHandler>((handler) => handler.Process(correspondenceId, null, cancellationToken), publishTime);
        }
    }
}
