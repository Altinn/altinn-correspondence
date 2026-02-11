using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Hangfire;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.SyncCorrespondenceEvent;

public class SyncCorrespondenceForwardingEventHandler(
    ICorrespondenceRepository correspondenceRepository,
    ICorrespondenceForwardingEventRepository forwardingEventRepository,
    IBackgroundJobClient backgroundJobClient,
    ILogger<SyncCorrespondenceForwardingEventHandler> logger) : IHandler<SyncCorrespondenceForwardingEventRequest, Guid>
{
    public async Task<OneOf<Guid, Error>> Process(SyncCorrespondenceForwardingEventRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var correspondence = await correspondenceRepository.GetCorrespondenceByIdForSync(
            request.CorrespondenceId,
            CorrespondenceSyncType.ForwardingEvents,
            cancellationToken);

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
                    fe.ForwardedOnDate.EqualsToSecond(syncedEvent.ForwardedOnDate)
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
                continue;
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
            await TransactionWithRetriesPolicy.Execute(async (canellationToken) =>
            {
                await forwardingEventRepository.AddForwardingEvents(forwardingEventsToExecute, cancellationToken);
                foreach(var forwardingEvent in forwardingEventsToExecute)
                {
                    backgroundJobClient.Enqueue<IDialogportenService>(service => service.AddForwardingEvent(forwardingEvent, cancellationToken));
                }
                return Task.CompletedTask;
            }, logger, cancellationToken);
        }

        return request.CorrespondenceId;
    }
}
