using Microsoft.Extensions.Logging;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Server;
using Altinn.Correspondence.Integrations.Slack;

namespace Altinn.Correspondence.Integrations.Hangfire
{
    public class SlackExceptionHandler : JobFilterAttribute, IServerFilter
    {
        private readonly SlackExceptionNotificationHandler _slackExceptionNotification;
        private readonly ILogger<SlackExceptionHandler> _logger;

        public SlackExceptionHandler(SlackExceptionNotificationHandler slackExceptionNotification, ILogger<SlackExceptionHandler> logger)
        {
            _slackExceptionNotification = slackExceptionNotification;
            _logger = logger;
        }

        public void OnPerforming(PerformingContext filterContext)
        {
            // Log the start of the job execution
            var jobId = filterContext.BackgroundJob.Id;
            var jobName = filterContext.BackgroundJob.Job.Type.Name;
            _logger.LogInformation("Starting job {JobId} of type {JobName}", jobId, jobName);
        }

        public async void OnPerformed(PerformedContext filterContext)
        {
            // Log the completion of the job execution
            var exception = filterContext.Exception;
            var jobId = filterContext.BackgroundJob.Id;
            var jobName = filterContext.BackgroundJob.Job.Type.Name;
            _logger.LogInformation("Completed job {JobId} of type {JobName}", jobId, jobName);
            
            // Get retry count from the job context
            var retryCount = filterContext.GetJobParameter<int>("RetryCount");
            
            // Properly await the notification
            if(exception != null) {
                await _slackExceptionNotification.TryHandleAsync(jobId, jobName, exception, retryCount, CancellationToken.None);
            }
        }

        public async void OnStateElection(ElectStateContext context)
        {
            if (context.CandidateState is FailedState failedState)
            {
                var exception = failedState.Exception;
                var jobId = context.BackgroundJob.Id;
                var jobName = context.BackgroundJob.Job.Type.Name;
                
                // Get retry count from the job context
                var retryCount = context.GetJobParameter<int>("RetryCount");

                _logger.LogError(exception, "Job {JobId} of type {JobName} failed", jobId, jobName);
                
                // Properly await the notification
                await _slackExceptionNotification.TryHandleAsync(jobId, jobName, exception, retryCount, CancellationToken.None);
            }
        }
    }
}