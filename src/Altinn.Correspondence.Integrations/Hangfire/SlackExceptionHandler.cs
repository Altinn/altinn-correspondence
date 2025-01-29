using Microsoft.Extensions.Logging;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using Hangfire.Server;
using System;
using System.Threading.Tasks;
using System.Threading;
using Altinn.Correspondence.Integrations.Slack;

namespace Altinn.Correspondence.Integrations.Hangfire
{
    public class SlackExceptionHandler : JobFilterAttribute, IServerFilter
    {
        private readonly SlackExceptionNotification _slackExceptionNotification;
        private readonly ILogger<SlackExceptionHandler> _logger;

        public SlackExceptionHandler(SlackExceptionNotification slackExceptionNotification, ILogger<SlackExceptionHandler> logger)
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

        public void OnPerformed(PerformedContext filterContext)
        {
            // Log the completion of the job execution
            var jobId = filterContext.BackgroundJob.Id;
            var jobName = filterContext.BackgroundJob.Job.Type.Name;
            _logger.LogInformation("Completed job {JobId} of type {JobName}", jobId, jobName);
        }

        public void OnStateElection(ElectStateContext context)
        {
            if (context.CandidateState is FailedState failedState)
            {
                var exception = failedState.Exception;
                var jobId = context.BackgroundJob.Id;
                var jobName = context.BackgroundJob.Job.Type.Name;

                _logger.LogError(exception, "Job {JobId} of type {JobName} failed", jobId, jobName);
                
                // Add this line to send the Slack notification
                _ = _slackExceptionNotification.TryHandleAsync(jobId, jobName, exception, CancellationToken.None);
            }
        }
    }
}