using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Integrations.Hangfire
{
    public class PublishCorrespondenceService
    {
        private readonly ILogger<PublishCorrespondenceService> _logger;
        private readonly ICorrespondenceRepository _correspondenceRepository;
        private readonly ICorrespondenceStatusRepository _correspondenceStatusRepository;
        private readonly IEventBus _eventBus;

        public PublishCorrespondenceService(
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
        public async Task Publish(Guid correspondenceId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Publish correspondence {correspondenceId}", correspondenceId);
            var correspondence = await _correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, cancellationToken);
            var errorMessage = "";
            if (correspondence == null)
            {
                errorMessage = "Correspondence " + correspondenceId + " not found when publishing";
            }
            else if (correspondence.Statuses.OrderByDescending(s => s.StatusChanged).First().Status != CorrespondenceStatus.ReadyForPublish)
            {
                errorMessage = $"Correspondence {correspondenceId} not ready for publish";
            }
            else if (correspondence.Content == null || correspondence.Content.Attachments.Any(a => a.Attachment?.Statuses.OrderByDescending(s => s.StatusChanged).First().Status != AttachmentStatus.Published))
            {
                errorMessage = $"Correspondence {correspondenceId} has attachments not published";
            }
            else if (correspondence.VisibleFrom > DateTimeOffset.UtcNow)
            {
                errorMessage = $"Correspondence {correspondenceId} not visible yet";
            }
            CorrespondenceStatusEntity status;
            AltinnEventType eventType = AltinnEventType.CorrespondencePublished;
            if (errorMessage.Length > 0)
            {
                _logger.LogError(errorMessage);
                status = new CorrespondenceStatusEntity
                {
                    CorrespondenceId = correspondenceId,
                    Status = CorrespondenceStatus.Failed,
                    StatusChanged = DateTimeOffset.UtcNow,
                    StatusText = errorMessage
                };
                eventType = AltinnEventType.CorrespondencePublishFailed;
            }
            else
            {
                status = new CorrespondenceStatusEntity
                {
                    CorrespondenceId = correspondenceId,
                    Status = CorrespondenceStatus.Published,
                    StatusChanged = DateTimeOffset.UtcNow,
                    StatusText = CorrespondenceStatus.Published.ToString()
                };

            }
            await _correspondenceStatusRepository.AddCorrespondenceStatus(status, cancellationToken);
            await _eventBus.Publish(eventType, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, cancellationToken);
            await _eventBus.Publish(eventType, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Recipient, cancellationToken);
        }

        [AutomaticRetry(Attempts = 0)]
        public Task DeletePublishJob(Guid correspondenceId)
        {
            _logger.LogInformation("Delete publish job for correspondence {correspondenceId}", correspondenceId);
            var monitor = JobStorage.Current.GetMonitoringApi();
            var jobsScheduled = monitor.ScheduledJobs(0, int.MaxValue)
                .Where(x => x.Value.Job.Method.Name == "Publish");
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