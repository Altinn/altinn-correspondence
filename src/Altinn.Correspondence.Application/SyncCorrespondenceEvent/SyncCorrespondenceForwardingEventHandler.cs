using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.SyncCorrespondenceEvent;

public class SyncCorrespondenceForwardingEventHandler(
    ICorrespondenceRepository correspondenceRepository,
    ICorrespondenceForwardingEventRepository forwardingEventRepository,
    ILogger<SyncCorrespondenceForwardingEventHandler> logger) : IHandler<SyncCorrespondenceForwardingEventRequest, Guid>
{
    public async Task<OneOf<Guid, Error>> Process(SyncCorrespondenceForwardingEventRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var correspondence = await correspondenceRepository.GetCorrespondenceById(
            request.CorrespondenceId,
            includeStatus: false,
            includeContent: false,
            includeForwardingEvents: true,
            cancellationToken,
            includeIsMigrating: true);

        if (correspondence == null)
        {
            logger.LogError("Correspondence {CorrespondenceId} not found", request.CorrespondenceId);
            return CorrespondenceErrors.CorrespondenceNotFound;
        }

        var forwardingEventsToExecute = new List<CorrespondenceForwardingEventEntity>();
        foreach (var syncedEvent in request.SyncedEvents)
        {
            if ((correspondence.ForwardingEvents ?? Enumerable.Empty<CorrespondenceForwardingEventEntity>())
                .Any(fe =>
                    fe.CorrespondenceId == request.CorrespondenceId
                    && fe.ForwardedOnDate.EqualsToSecond(syncedEvent.ForwardedOnDate)
                    && fe.ForwardedByPartyUuid == syncedEvent.ForwardedByPartyUuid
                    && fe.ForwardedByUserUuid == syncedEvent.ForwardedByUserUuid
                    && fe.ForwardedToUserId == syncedEvent.ForwardedToUserId
                    && fe.ForwardedToUserUuid == syncedEvent.ForwardedToUserUuid
                    && fe.ForwardedToEmailAddress == syncedEvent.ForwardedToEmailAddress
                    && fe.ForwardingText == syncedEvent.ForwardingText
                    && fe.MailboxSupplier == syncedEvent.MailboxSupplier
                    ))
            {                
                logger.LogWarning("Forwarding event already exists for correspondence {CorrespondenceId}. Skipping sync.", request.CorrespondenceId);
                continue; // Skip already existing events
            }
            else
            {
                forwardingEventsToExecute.Add(syncedEvent);
            }
        }

        if (forwardingEventsToExecute.Count == 0)
        {
            logger.LogInformation("No new forwarding events to sync for correspondence {CorrespondenceId}", request.CorrespondenceId);
            return request.CorrespondenceId;
        }
        else
        {
            foreach (var forwardingEvent in forwardingEventsToExecute)
            {
                forwardingEvent.CorrespondenceId = request.CorrespondenceId;
                forwardingEvent.SyncedFromAltinn2 = DateTimeOffset.UtcNow;
            }

            // Add the new forwarding event to the repository
            await forwardingEventRepository.AddForwardingEvents(forwardingEventsToExecute, cancellationToken);
        }

        return request.CorrespondenceId;
    }
}
