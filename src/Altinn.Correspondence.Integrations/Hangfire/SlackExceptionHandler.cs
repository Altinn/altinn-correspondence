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
            // No action needed before the job is performed
        }

        public void OnPerformed(PerformedContext filterContext)
        {
            // No action needed after the job is performed
        }

        public void OnStateElection(ElectStateContext context)
        {
            if (context.CandidateState is FailedState failedState)
            {
                var exception = failedState.Exception;
                var jobId = context.BackgroundJob.Id;
                var jobName = context.BackgroundJob.Job.Type.Name;

                _logger.LogError(exception, "Job {JobId} of type {JobName} failed", jobId, jobName);

                // Send the exception details to Slack
                Task.Run(() => _slackExceptionNotification.TryHandleAsync(jobId, jobName, exception, CancellationToken.None));
            }
        }
    }
}