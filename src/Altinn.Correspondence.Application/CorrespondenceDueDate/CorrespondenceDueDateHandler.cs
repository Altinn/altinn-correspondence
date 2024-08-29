using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Application.CorrespondenceDueDate
{
    public class CorrespondenceDueDateHandler
    {
        private readonly ILogger<CorrespondenceDueDateHandler> _logger;
        private readonly ICorrespondenceRepository _correspondenceRepository;
        private readonly ICorrespondenceStatusRepository _correspondenceStatusRepository;
        private readonly IEventBus _eventBus;

        public CorrespondenceDueDateHandler(
            ILogger<CorrespondenceDueDateHandler> logger,
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
        public async Task Process(Guid correspondenceId, CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Due date for correspondence {correspondenceId} has expired", correspondenceId);
            var correspondence = await _correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, cancellationToken);
            if (correspondence == null)
            {
                throw new Exception("Correspondence " + correspondenceId + " not found for exipired due date");
            }
            else if (correspondence.Content == null || !correspondence.Statuses.Any(s => s.Status == CorrespondenceStatus.Published))
            {
                throw new Exception($"Correspondence {correspondenceId} was never published");
            }
            else if (correspondence.Statuses.Any(s => s.Status == CorrespondenceStatus.Failed))
            {
                throw new Exception($"Correspondence {correspondenceId} failed to publish");
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
    }
}