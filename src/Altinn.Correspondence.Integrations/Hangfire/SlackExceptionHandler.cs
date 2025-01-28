using Hangfire.Common;
using Hangfire.States;
using Hangfire.Storage;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Altinn.Correspondence.Helpers;

namespace Altinn.Correspondence.Integrations.Hangfire
{
    public class SlackExceptionHandler : JobFilterAttribute, IServerFilter
    {
        private readonly SlackExceptionNotification _SlackExceptionNotification;
        private readonly ILogger<SlackExceptionHandler> _logger;

        public SlackExceptionHandler(SlackExceptionNotification SlackExceptionNotification, ILogger<SlackExceptionHandler> logger)
        {
            _SlackExceptionNotification = SlackExceptionNotification;
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
                Task.Run(() => _SlackExceptionNotification.NotifyAsync(exception, $"Job {jobId} of type {jobName}"));
            }
        }
    }
}