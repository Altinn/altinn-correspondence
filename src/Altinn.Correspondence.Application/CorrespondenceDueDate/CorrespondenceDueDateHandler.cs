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
        IEventBus eventBus)
    {

        [AutomaticRetry(Attempts = 0)]
        public async Task Process(Guid correspondenceId, CancellationToken cancellationToken = default)
        {
            try
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
                    await eventBus.Publish(AltinnEventType.CorrespondenceReceiverNeverRead, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, cancellationToken);
                    await eventBus.Publish(AltinnEventType.CorrespondenceReceiverNeverRead, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Recipient, cancellationToken);
                }
                if (correspondence.IsConfirmationNeeded && !correspondence.StatusHasBeen(CorrespondenceStatus.Confirmed))
                {
                    await eventBus.Publish(AltinnEventType.CorrespondenceReceiverNeverConfirmed, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, cancellationToken);
                    await eventBus.Publish(AltinnEventType.CorrespondenceReceiverNeverConfirmed, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Recipient, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error processing due date for correspondence {correspondenceId}", correspondenceId);
                throw; 
            }
        }
    }
}