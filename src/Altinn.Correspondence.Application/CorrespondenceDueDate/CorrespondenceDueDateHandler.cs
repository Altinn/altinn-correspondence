using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Application.CorrespondenceDueDate
{
    public class CorrespondenceDueDateHandler(
        ILogger<CorrespondenceDueDateHandler> logger,
        ICorrespondenceRepository correspondenceRepository,
        IBackgroundJobClient backgroundJobClient,
        IEventBus eventBus)
    {

        [AutomaticRetry(Attempts = 0)]
        public async Task Process(Guid correspondenceId, CancellationToken cancellationToken = default)
        {
                logger.LogInformation("Due date for correspondence {correspondenceId} has expired", correspondenceId);

                var correspondence = await correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, cancellationToken);
                if (correspondence == null)
                {
                    throw new Exception("Correspondence " + correspondenceId + " not found for exipired due date");
                }
                else if (correspondence.Content == null || !correspondence.StatusHasBeen(CorrespondenceStatus.Published))
                {
                    throw new Exception($"Correspondence {correspondenceId} was never published");
                }
                else if (correspondence.StatusHasBeen(CorrespondenceStatus.Failed))
                {
                    throw new Exception($"Correspondence {correspondenceId} failed to publish");
                }

                if (!correspondence.StatusHasBeen(CorrespondenceStatus.Read))
                {
                    backgroundJobClient.Enqueue(() => eventBus.Publish(AltinnEventType.CorrespondenceReceiverNeverRead, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, cancellationToken));
                    backgroundJobClient.Enqueue(() => eventBus.Publish(AltinnEventType.CorrespondenceReceiverNeverRead, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Recipient, cancellationToken));
                }
                if (correspondence.IsConfirmationNeeded && !correspondence.StatusHasBeen(CorrespondenceStatus.Confirmed))
                {
                    backgroundJobClient.Enqueue(() => eventBus.Publish(AltinnEventType.CorrespondenceReceiverNeverConfirmed, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, cancellationToken));
                    backgroundJobClient.Enqueue(() => eventBus.Publish(AltinnEventType.CorrespondenceReceiverNeverConfirmed, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Recipient, cancellationToken));
                }
        }
    }
}