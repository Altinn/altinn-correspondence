using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.UpdateCorrespondenceStatus;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.SyncCorrespondenceEvent;

public class SyncCorrespondenceStatusEventHandler(
    ICorrespondenceRepository correspondenceRepository,
    ICorrespondenceDeleteEventRepository correspondenceDeleteEventRepository,
    UpdateCorrespondenceStatusHelper updateCorrespondenceStatusHelper,
    SyncCorrespondenceStatusEventHelper syncCorrespondenceStatusHelper,
    ILogger<SyncCorrespondenceStatusEventHandler> logger) : IHandler<SyncCorrespondenceStatusEventRequest, Guid>
{
    public async Task<OneOf<Guid, Error>> Process(SyncCorrespondenceStatusEventRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        int numSyncedEvents = request.SyncedEvents?.Count ?? 0;
        int numSyncedDeletes = request.SyncedDeleteEvents?.Count ?? 0;

        logger.LogInformation("Processing status Sync request for correspondence {CorrespondenceId} with {numSyncedEvents} # status events and {numSyncedDeletes} # delete events", request.CorrespondenceId, numSyncedEvents, numSyncedDeletes);

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

        if (numSyncedEvents > 0)
        {
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
            if (eventsFilteredForDuplicates.Count > 0)
            {
                await TransactionWithRetriesPolicy.Execute<Guid>(async (cancellationToken) =>
                {
                    await syncCorrespondenceStatusHelper.AddSyncedCorrespondenceStatuses(correspondence, eventsFilteredForDuplicates, cancellationToken);

                    if (correspondence.IsMigrating == false)
                    {
                        foreach (var eventToExecute in eventsFilteredForDuplicates)
                        {
                            updateCorrespondenceStatusHelper.ReportActivityToDialogporten(request.CorrespondenceId, eventToExecute.Status, eventToExecute.StatusChanged); // Set the operationtime to the time the status was changed in Altinn 2
                            updateCorrespondenceStatusHelper.PatchCorrespondenceDialog(request.CorrespondenceId, eventToExecute.Status);
                            updateCorrespondenceStatusHelper.PublishEvent(correspondence, eventToExecute.Status);
                            if (eventToExecute.Status == CorrespondenceStatus.Archived)
                            {
                                await syncCorrespondenceStatusHelper.ReportArchivedToDialogporten(request.CorrespondenceId, eventToExecute.PartyUuid, cancellationToken);
                            }
                            else if (eventToExecute.Status == CorrespondenceStatus.Read)
                            {
                                syncCorrespondenceStatusHelper.ReportReadToDialogporten(request.CorrespondenceId, eventToExecute.StatusChanged);
                            }
                        }
                    }

                    return request.CorrespondenceId;
                }, logger, cancellationToken);
            }
        }

        // Handle deletion events individually
        if (numSyncedDeletes > 0)
        {
            var deletionEventsFilteredForRequestDuplicates = FilterDuplicateDeleteEvents(request.SyncedDeleteEvents);

            var deletionEventsInDatabase = await correspondenceDeleteEventRepository.GetDeleteEventsForCorrespondenceId(request.CorrespondenceId, cancellationToken);

            var deletionEventsFilteredForDuplicates = new List<CorrespondenceDeleteEventEntity>();
            foreach (var deletionEventToSync in deletionEventsFilteredForRequestDuplicates)
            {
                if (deletionEventsInDatabase.Any(
                    e => e.EventType == deletionEventToSync.EventType
                    && e.EventOccurred.EqualsToSecond(deletionEventToSync.EventOccurred)
                    && e.PartyUuid == deletionEventToSync.PartyUuid)
                    )
                {
                    logger.LogInformation("Current Deletion Event for {CorrespondenceId} has been deemed duplicate of existing and will be skipped. EventType: {EventType} - EventOccurred: {EventOccurred} - PartyUuid: {PartyUuid}", request.CorrespondenceId, deletionEventToSync.EventType, deletionEventToSync.EventOccurred, deletionEventToSync.PartyUuid);
                    continue;
                }
                else
                {
                    deletionEventsFilteredForDuplicates.Add(deletionEventToSync);
                }
            }

            // Sort by EventOccurred ascending
            var sortedDeletionEvents = deletionEventsFilteredForDuplicates
                .OrderBy(e => e.EventOccurred)
                .ToList();

            foreach (var deletionEvent in sortedDeletionEvents)
            {
                switch(deletionEvent.EventType)
                {
                    case CorrespondenceDeleteEventType.HardDeletedByServiceOwner:
                    case CorrespondenceDeleteEventType.HardDeletedByRecipient:
                        await syncCorrespondenceStatusHelper.PurgeCorrespondence(correspondence, deletionEvent, cancellationToken);
                        break;
                    case CorrespondenceDeleteEventType.SoftDeletedByRecipient:
                    case CorrespondenceDeleteEventType.RestoredByRecipient:
                        await syncCorrespondenceStatusHelper.SetSoftDeleteOrRestoreOnDialog(correspondence, deletionEvent, cancellationToken);
                        break;
                    default:
                        logger.LogWarning("Unknown Deletion Event Type {EventType} for Correspondence {CorrespondenceId}. The event will be ignored.", deletionEvent.EventType, request.CorrespondenceId);
                        break;
                }
            }
        }

        logger.LogInformation("Successfully synced request for correspondence {CorrespondenceId} with {numSyncedEvents} # status events and {numSyncedDeletes} # delete events", request.CorrespondenceId, numSyncedEvents, numSyncedDeletes);
        return request.CorrespondenceId;
    }

    private static List<CorrespondenceStatusEntity> FilterDuplicateStatusEvents(List<CorrespondenceStatusEntity> input)
    {
        var exists = new HashSet<(CorrespondenceStatus Status, DateTimeOffset TruncatedStatusChanged, Guid PartyUuid)>();
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

            if (exists.Add(key))
            {
                result.Add(item);
            }
        }
        return result;
    }

    private static List<CorrespondenceDeleteEventEntity> FilterDuplicateDeleteEvents(List<CorrespondenceDeleteEventEntity> input)
    {
        var exists = new HashSet<(CorrespondenceDeleteEventType EventType, DateTimeOffset TruncatedEventOccurred, Guid PartyUuid)>();
        var result = new List<CorrespondenceDeleteEventEntity>();

        foreach (var item in input)
        {
            var key = (
                item.EventType,
                new DateTimeOffset(
                    item.EventOccurred.ToUniversalTime().Year,
                    item.EventOccurred.ToUniversalTime().Month,
                    item.EventOccurred.ToUniversalTime().Day,
                    item.EventOccurred.ToUniversalTime().Hour,
                    item.EventOccurred.ToUniversalTime().Minute,
                    item.EventOccurred.ToUniversalTime().Second,
                    TimeSpan.Zero
                ),
                item.PartyUuid
            );

            if (exists.Add(key))
            {
                result.Add(item);
            }
        }
        return result;
    }
}
