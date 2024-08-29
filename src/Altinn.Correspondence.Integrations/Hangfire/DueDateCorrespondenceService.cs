using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Integrations.Hangfire
{
    public class DueDateCorrespondenceService
    {
        private readonly ILogger<PublishCorrespondenceService> _logger;
        private readonly ICorrespondenceRepository _correspondenceRepository;
        private readonly ICorrespondenceStatusRepository _correspondenceStatusRepository;
        private readonly IEventBus _eventBus;

        public DueDateCorrespondenceService(
            ILogger<PublishCorrespondenceService> logger,
            ICorrespondenceRepository correspondenceRepository,
            ICorrespondenceStatusRepository correspondenceStatusRepository,
            IEventBus eventBus)
        {
            _logger = logger;
            _correspondenceRepository = correspondenceRepository;
            _correspondenceStatusRepository = correspondenceStatusRepository;
            _eventBus = eventBus;
        }

        [AutomaticRetry(Attempts = 0)]
        public async Task ProcessDueDate(Guid correspondenceId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Due date for correspondence {correspondenceId} has expired", correspondenceId);
            var correspondence = await _correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, cancellationToken);
            var errorMessage = "";
            if (correspondence == null)
            {
                errorMessage = "Correspondence " + correspondenceId + " not found for exipired due date";
            }
            else if (correspondence.Content == null || !correspondence.Statuses.Any(s => s.Status == CorrespondenceStatus.Published))
            {
                errorMessage = $"Correspondence {correspondenceId} was never published";
            }
            if (correspondence.DueDateTime > DateTimeOffset.UtcNow)
            {
                errorMessage = $"Correspondence {correspondenceId} due date has not expired";
            }
            if (!string.IsNullOrEmpty(errorMessage))
            {
                _logger.LogError(errorMessage);
                return;
            }

            if (!correspondence.Statuses.Any(s => s.Status != CorrespondenceStatus.Read))
            {
                await _eventBus.Publish(AltinnEventType.CorrespondenceReceiverNeverRead, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, cancellationToken);
                await _eventBus.Publish(AltinnEventType.CorrespondenceReceiverNeverRead, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Recipient, cancellationToken);
            }
            if (!correspondence.Statuses.Any(s => s.Status == CorrespondenceStatus.Confirmed)) // TODO: add logic to only check if confirmation is required
            {
                await _eventBus.Publish(AltinnEventType.CorrespondenceReceiverNeverConfirmed, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, cancellationToken);
                await _eventBus.Publish(AltinnEventType.CorrespondenceReceiverNeverConfirmed, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Recipient, cancellationToken);
            }
        }

        [AutomaticRetry(Attempts = 0)]
        public Task DeleteDueDateJob(Guid correspondenceId)
        {
            _logger.LogInformation("Delete Due date  job for correspondence {correspondenceId}", correspondenceId);
            var monitor = JobStorage.Current.GetMonitoringApi();
            var jobsScheduled = monitor.ScheduledJobs(0, int.MaxValue)
                .Where(x => x.Value.Job.Method.Name == "ProcessDueDate");
            foreach (var j in jobsScheduled)
            {
                var t = (Guid)j.Value.Job.Args[0];
                if (t == correspondenceId)
                {
                    BackgroundJob.Delete(j.Key);
                }
            }
            return Task.CompletedTask;
        }
    }

}