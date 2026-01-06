using Microsoft.Extensions.Logging;
using Hangfire.Common;
using Hangfire;
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
            var origin = filterContext.GetJobParameter<string>("Origin");
            
            // Properly await the notification
            if(exception != null) {
                if (string.Equals(origin, "migrate", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Skipping Slack exception notification for migration related job {JobId}", jobId);
                    return;
                }
                if (IsCheckNotificationDeliveryJob(filterContext.BackgroundJob.Job) && !IsLastRetryAttempt(filterContext.BackgroundJob.Job, retryCount))
                {
                    _logger.LogInformation("Skipping Slack exception notification for CheckNotificationDelivery job attempt (jobId {JobId}, retryCount {RetryCount})", jobId, retryCount);
                    return;
                }
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
                var origin = context.GetJobParameter<string>("Origin");

                _logger.LogError(exception, "Job {JobId} of type {JobName} failed", jobId, jobName);
                
                // Properly await the notification
                if (string.Equals(origin, "migrate", StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Skipping Slack exception notification for migrate related job {JobId}", jobId);
                    return;
                }
                if (IsCheckNotificationDeliveryJob(context.BackgroundJob.Job) && !IsLastRetryAttempt(context.BackgroundJob.Job, retryCount))
                {
                    _logger.LogInformation("Skipping Slack exception notification for CheckNotificationDelivery job attempt (jobId {JobId}, retryCount {RetryCount})", jobId, retryCount);
                    return;
                }
                await _slackExceptionNotification.TryHandleAsync(jobId, jobName, exception, retryCount, CancellationToken.None);
            }
        }

        private static bool IsCheckNotificationDeliveryJob(Job job)
        {
            return job.Type.FullName?.EndsWith(".CheckNotificationDeliveryHandler", StringComparison.Ordinal) == true
                   && string.Equals(job.Method?.Name, "Process", StringComparison.Ordinal);
        }

        private static bool IsLastRetryAttempt(Job job, int retryCount)
        {
            var attempts = GetConfiguredRetryAttempts(job) ?? 10; // Hangfire default
            return attempts > 0 && retryCount >= attempts - 1;
        }

        private static int? GetConfiguredRetryAttempts(Job job)
        {
            var methodAttr = job.Method.GetCustomAttributes(typeof(AutomaticRetryAttribute), inherit: true)
                .OfType<AutomaticRetryAttribute>()
                .FirstOrDefault();
            if (methodAttr != null)
            {
                return methodAttr.Attempts;
            }

            var typeAttr = job.Type.GetCustomAttributes(typeof(AutomaticRetryAttribute), inherit: true)
                .OfType<AutomaticRetryAttribute>()
                .FirstOrDefault();
            return typeAttr?.Attempts;
        }
    }
}