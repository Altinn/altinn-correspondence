using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Hangfire;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.SyncCorrespondenceEvent;

public class SyncCorrespondenceStatusEventHandler(
    ICorrespondenceRepository correspondenceRepository,
    SyncCorrespondenceStatusEventHelper syncCorrespondenceStatusHelper,
    IBackgroundJobClient backgroundJobClient,
    ILogger<SyncCorrespondenceStatusEventHandler> logger) : IHandler<SyncCorrespondenceStatusEventRequest, Guid>
{
    public async Task<OneOf<Guid, Error>> Process(SyncCorrespondenceStatusEventRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing status Sync request for correspondence {CorrespondenceId} with {SyncedEventsCount} # of status events", request.CorrespondenceId, request.SyncedEvents.Count);

        var correspondence = await correspondenceRepository.GetCorrespondenceById(
            request.CorrespondenceId,
            includeStatus: true,
            includeContent: false,
            includeForwardingEvents: false,
            cancellationToken,
            includeIsMigrating: true);

        if (correspondence == null)
        {
            logger.LogWarning("Correspondence {CorrespondenceId} not found", request.CorrespondenceId);
            return CorrespondenceErrors.CorrespondenceNotFound;
        }

        var eventsFilteredForCorrectStatus = new List<CorrespondenceStatusEntity>();
        {
            foreach (var statusEventToSync in request.SyncedEvents)
            {
                // Validate if the status event is valid for this handler / sync operation (unlikely, but possible)
                if (syncCorrespondenceStatusHelper.ValidateStatusUpdate(statusEventToSync))
                {
                    eventsFilteredForCorrectStatus.Add(statusEventToSync);
                }
                else
                {
                    logger.LogInformation(
                        "Status Event for {CorrespondenceId} has been deemed invalid and will be ignored. Status: {Status} - StatusChanged: {StatusChanged} - PartyUuid: {PartyUuid}",
                        request.CorrespondenceId,
                        statusEventToSync.Status,
                        statusEventToSync.StatusChanged,
                        statusEventToSync.PartyUuid);
                }
            }
        }
        if (eventsFilteredForCorrectStatus.Count == 0)
        {
            logger.LogWarning("None of the Status Events for {CorrespondenceId} has been deemed valid and no sync will be performed.", request.CorrespondenceId);
            return request.CorrespondenceId;
        }

        // Remove possible duplicates from the request - This is because Altinn 2 uses two sets of data sources for status events, and we need to ensure that we only sync unique events.
        var eventsFilteredForRequestDuplicates = FilterDuplicateStatusEvents(eventsFilteredForCorrectStatus);

        // Remove duplicate status events that are already present in the correspondence
        var eventsFilteredForDuplicates = new List<CorrespondenceStatusEntity>();
        foreach (var syncedEvent in eventsFilteredForRequestDuplicates)
        {
            if (correspondence.Statuses.Any(
                s => s.Status == syncedEvent.Status
                && s.StatusChanged.EqualsToSecond(syncedEvent.StatusChanged)
                && s.PartyUuid == syncedEvent.PartyUuid)
                )
            {
                logger.LogInformation("Current Status Event for {CorrespondenceId} has been deemed duplicate of existing and will be skipped. Status: {Status} - StatusChanged: {StatusChanged} - PartyUuid: {PartyUuid}", request.CorrespondenceId, syncedEvent.Status, syncedEvent.StatusChanged, syncedEvent.PartyUuid);
                continue;
            }
            else
            {
                eventsFilteredForDuplicates.Add(syncedEvent);
            }
        }
        if (eventsFilteredForDuplicates.Count == 0)
        {
            logger.LogWarning("None of the Status Events for {CorrespondenceId} were unique, and no sync will be performed.", request.CorrespondenceId);
            return request.CorrespondenceId;
        }

        logger.LogInformation("Executing status sync transaction for correspondence for {CorrespondenceId} with {SyncedEventsCount} # of status events", request.CorrespondenceId, eventsFilteredForDuplicates.Count);

        // Special case for Purge events, we need to handle them differently
        var purgeEvent = eventsFilteredForDuplicates
        .FirstOrDefault(e =>
            e.Status == Core.Models.Enums.CorrespondenceStatus.PurgedByRecipient ||
            e.Status == Core.Models.Enums.CorrespondenceStatus.PurgedByAltinn);
        if (purgeEvent != null)
        {
            var alreadyPurged = correspondence.GetPurgedStatus();
            if (alreadyPurged is not null)
            {
                logger.LogInformation("Current Status Event for {CorrespondenceId} is a Purge Event, but Correspondence has already been purged, so skipping action.", request.CorrespondenceId);
            }
            else
            {
                logger.LogInformation("Purge Correspondence based on sync Event from Altinn 2: {CorrespondenceId}", request.CorrespondenceId);
                await syncCorrespondenceStatusHelper.PurgeCorrespondence(correspondence, purgeEvent, correspondence.IsMigrating == false, cancellationToken);
            }
            eventsFilteredForDuplicates.Remove(purgeEvent);
        }

        // Handle the rest of the status events collectively
        if (eventsFilteredForDuplicates.Count > 0)
        {
            await TransactionWithRetriesPolicy.Execute<Guid>(async (cancellationToken) =>
            {
                await syncCorrespondenceStatusHelper.AddSyncedCorrespondenceStatuses(correspondence, eventsFilteredForDuplicates, cancellationToken);

                if (correspondence.IsMigrating == false)
                {
                    foreach (var eventToExecute in eventsFilteredForDuplicates)
                    {
                        if (eventToExecute.Status == CorrespondenceStatus.Confirmed)
                        {
                            backgroundJobClient.Enqueue<IDialogportenService>((dialogportenService) => dialogportenService.CreateConfirmedActivity(request.CorrespondenceId, DialogportenActorType.Recipient, eventToExecute.StatusChanged)); // Set the operationtime to the time the status was changed in Altinn 2;
                            backgroundJobClient.Enqueue<IDialogportenService>((dialogportenService) => dialogportenService.PatchCorrespondenceDialogToConfirmed(request.CorrespondenceId));
                            backgroundJobClient.Enqueue<IEventBus>((eventBus) => eventBus.Publish(AltinnEventType.CorrespondenceReceiverConfirmed, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, CancellationToken.None));
                        }
                        else if (eventToExecute.Status == CorrespondenceStatus.Read)
                        {
                            backgroundJobClient.Enqueue<IEventBus>((eventBus) => eventBus.Publish(AltinnEventType.CorrespondenceReceiverRead, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, CancellationToken.None));
                        }
                        else if (eventToExecute.Status == CorrespondenceStatus.Archived)
                        {
                            await syncCorrespondenceStatusHelper.ReportArchivedToDialogporten(request.CorrespondenceId, eventToExecute.PartyUuid, cancellationToken);
                        }
                    }
                }

                return request.CorrespondenceId;
            }, logger, cancellationToken);
        }
        

        logger.LogInformation($"Successfully synced request for correspondence {request.CorrespondenceId} with {request.SyncedEvents.Count} # of status events");
        return request.CorrespondenceId;
    }

    private static List<CorrespondenceStatusEntity> FilterDuplicateStatusEvents(List<CorrespondenceStatusEntity> input)
    {
        var seen = new HashSet<(CorrespondenceStatus Status, DateTimeOffset TruncatedStatusChanged, Guid PartyUuid)>();
        var result = new List<CorrespondenceStatusEntity>();

        foreach (var item in input)
        {
            var key = (
                item.Status,
                new DateTimeOffset(
                    item.StatusChanged.ToUniversalTime().Year,
                    item.StatusChanged.ToUniversalTime().Month,
                    item.StatusChanged.ToUniversalTime().Day,
                    item.StatusChanged.ToUniversalTime().Hour,
                    item.StatusChanged.ToUniversalTime().Minute,
                    item.StatusChanged.ToUniversalTime().Second,
                    TimeSpan.Zero
                ),
                item.PartyUuid
            );

            if (seen.Add(key))
            {
                result.Add(item);
            }
        }
        return result;
    }
}
