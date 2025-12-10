using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.PurgeCorrespondence;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.SyncCorrespondenceEvent;

public class SyncCorrespondenceStatusEventHandler(
    ICorrespondenceRepository correspondenceRepository,
    ICorrespondenceStatusRepository correspondenceStatusRepository,
    ICorrespondenceDeleteEventRepository correspondenceDeleteEventRepository,
    IAltinnRegisterService altinnRegisterService,
    PurgeCorrespondenceHelper purgeCorrespondenceHelper,
    IBackgroundJobClient backgroundJobClient,
    ILogger<SyncCorrespondenceStatusEventHandler> logger) : IHandler<SyncCorrespondenceStatusEventRequest, Guid>
{
    public async Task<OneOf<Guid, Error>> Process(SyncCorrespondenceStatusEventRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        int numSyncedEvents = request.SyncedEvents?.Count ?? 0;
        int numSyncedDeletes = request.SyncedDeleteEvents?.Count ?? 0;

        logger.LogInformation("Processing status Sync request for correspondence {CorrespondenceId} with {numSyncedEvents} status events and {numSyncedDeletes} delete events", 
            request.CorrespondenceId, numSyncedEvents, numSyncedDeletes);

        if (numSyncedEvents == 0 && numSyncedDeletes == 0)
        {
            logger.LogInformation("No events to sync for Correspondence {CorrespondenceId}. Exiting sync process.", request.CorrespondenceId);
            return request.CorrespondenceId;
        }

        // Only fetch Dialogporten enduserIds if we have events 
        Dictionary<Guid, string> enduserIdByPartyUuid = enduserIdByPartyUuid = await GetDialogPortenEndUserIdsForEvents(request.SyncedEvents, request.SyncedDeleteEvents, cancellationToken);

        var txResult = await TransactionWithRetriesPolicy.Execute<Guid>(async (cancellationToken) =>
        {
            // Fetch correspondence with fresh data within transaction
            var correspondence = await correspondenceRepository.GetCorrespondenceByIdForSync(
                request.CorrespondenceId,
                CorrespondenceSyncType.StatusEvents,
                cancellationToken);

            if (correspondence == null)
            {
                logger.LogWarning("Correspondence {CorrespondenceId} not found", request.CorrespondenceId);
                return CorrespondenceErrors.CorrespondenceNotFound;
            }

            var statusEventsToProcess = new List<CorrespondenceStatusEntity>();
            var deletionEventsToProcess = new List<CorrespondenceDeleteEventEntity>();

            if (numSyncedEvents > 0)
            {
                statusEventsToProcess = FilterStatusEvents(request, correspondence);
                if (statusEventsToProcess.Count == 0)
                {
                    logger.LogWarning("None of the Status Events for {CorrespondenceId} were unique, and no sync will be performed.", request.CorrespondenceId);
                }
            }

            if (numSyncedDeletes > 0)
            {
                deletionEventsToProcess = await FilterDeleteEvents(request, cancellationToken);
                if (deletionEventsToProcess.Count == 0)
                {
                    logger.LogWarning("None of the Delete Events for {CorrespondenceId} were unique, and no sync will be performed.", request.CorrespondenceId);
                }
            }

            if (deletionEventsToProcess.Count == 0 && statusEventsToProcess.Count == 0)
            {
                logger.LogInformation("No unique Status or Delete Events to sync for Correspondence {CorrespondenceId}. Exiting sync process.", request.CorrespondenceId);
                return request.CorrespondenceId;
            }

            // After filtering both collections, combine them into a single sorted collection, sorted by timestamp they occurred
            var allEventsToProcess = statusEventsToProcess
                .Select(e => new { EventType = "Status", Event = (object)e, Timestamp = e.StatusChanged })
                .Concat(deletionEventsToProcess
                    .Select(e => new { EventType = "Delete", Event = (object)e, Timestamp = e.EventOccurred }))
                .OrderBy(e => e.Timestamp)
                .ToList();

            // Process events sequentially by chronological order to maintain granular control
            foreach (var evt in allEventsToProcess)
            {
                logger.LogInformation("Processing {EventType} event for correspondence {CorrespondenceId} at {Timestamp}", 
                    evt.EventType, request.CorrespondenceId, evt.Timestamp);
                
                try
                {
                    if (evt.EventType == "Status")
                    {
                        await ProcessStatusEvent(request, correspondence, enduserIdByPartyUuid, (CorrespondenceStatusEntity)evt.Event, cancellationToken);
                    }
                    else if (evt.EventType == "Delete")
                    {
                        await ProcessDeleteEvent(request, correspondence, enduserIdByPartyUuid, (CorrespondenceDeleteEventEntity)evt.Event, cancellationToken);
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to process {EventType} event for correspondence {CorrespondenceId} at {Timestamp}", 
                        evt.EventType, request.CorrespondenceId, evt.Timestamp);
                    throw; // Re-throw to trigger transaction rollback
                }
            }

            logger.LogInformation("Successfully processed {TotalEvents} events for correspondence {CorrespondenceId}", 
                allEventsToProcess.Count, request.CorrespondenceId);

            return request.CorrespondenceId;
        }, logger, cancellationToken);

        return txResult.Match<OneOf<Guid, Error>>(
            success => 
            {
                logger.LogInformation("Successfully synced request for correspondence {CorrespondenceId} with {numSyncedEvents} status events and {numSyncedDeletes} delete events", 
                    request.CorrespondenceId, numSyncedEvents, numSyncedDeletes);
                return success;
            },
            error => 
            {
                logger.LogWarning("Failed to sync request for correspondence {CorrespondenceId}: {Error}", 
                    request.CorrespondenceId, error);
                return error;
            });
    }

    private async Task ProcessStatusEvent(SyncCorrespondenceStatusEventRequest request, CorrespondenceEntity correspondence, Dictionary<Guid, string> enduserIdByPartyUuid, CorrespondenceStatusEntity eventToExecute, CancellationToken cancellationToken)
    {
        logger.LogDebug("Process Sync status event {Status} for {CorrespondenceId}", eventToExecute.Status, request.CorrespondenceId);
        
        // Execute background jobs first (these are queued, not executed immediately)
        if (correspondence.IsMigrating == false)
        {
            switch (eventToExecute.Status)
            {
                case CorrespondenceStatus.Confirmed:
                    {
                        var patchJobId = backgroundJobClient.Enqueue<IDialogportenService>((dialogportenService) => dialogportenService.PatchCorrespondenceDialogToConfirmed(request.CorrespondenceId));
                        backgroundJobClient.ContinueJobWith<IDialogportenService>(patchJobId, (dialogportenService) => dialogportenService.CreateConfirmedActivity(request.CorrespondenceId, DialogportenActorType.Recipient, eventToExecute.StatusChanged), JobContinuationOptions.OnlyOnSucceededState); // Set the operationtime to the time the status was changed in Altinn 2;
                        backgroundJobClient.Enqueue<IEventBus>((eventBus) => eventBus.Publish(AltinnEventType.CorrespondenceReceiverConfirmed, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, CancellationToken.None));
                        break;
                    }

                case CorrespondenceStatus.Read:
                    {
                        backgroundJobClient.Enqueue<IDialogportenService>((dialogportenService) => dialogportenService.CreateOpenedActivity(correspondence.Id, DialogportenActorType.Recipient, eventToExecute.StatusChanged));
                        backgroundJobClient.Enqueue<IEventBus>((eventBus) => eventBus.Publish(AltinnEventType.CorrespondenceReceiverRead, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, CancellationToken.None));
                        break;
                    }

                case CorrespondenceStatus.Archived:
                    {
                        if (!enduserIdByPartyUuid.ContainsKey(eventToExecute.PartyUuid))
                        {
                            logger.LogWarning("Skipping updating dialog with SystemLabel Archived for correspondence {CorrespondenceId} at {StatusChanged} due to missing Dialogporten enduserId for party {PartyUuid}.", correspondence.Id, eventToExecute.StatusChanged, eventToExecute.PartyUuid);
                        }
                        else
                        {
                            backgroundJobClient.Enqueue<IDialogportenService>(service => service.UpdateSystemLabelsOnDialog(correspondence.Id, enduserIdByPartyUuid[eventToExecute.PartyUuid], new List<DialogPortenSystemLabel> { DialogPortenSystemLabel.Archive }, null));
                        }                        
                        break;
                    }
                default:
                    logger.LogWarning("Unsupported Status Event type {Status} for Correspondence {CorrespondenceId}. The event will be ignored.", eventToExecute.Status, request.CorrespondenceId);
                    break;
            }
        }

        // Save status to Correspondence Database - this is the critical database operation that must succeed within the transaction
        await StoreStatusEventAsCorrespondenceStatus(correspondence, eventToExecute, DateTimeOffset.UtcNow, cancellationToken);
    }

    private async Task ProcessDeleteEvent(SyncCorrespondenceStatusEventRequest request, CorrespondenceEntity correspondence, Dictionary<Guid, string> enduserIdByPartyUuid, CorrespondenceDeleteEventEntity deletionEvent, CancellationToken cancellationToken)
    {
        logger.LogDebug("Process Sync delete event {EventType} for {CorrespondenceId}", deletionEvent.EventType, request.CorrespondenceId);
        switch (deletionEvent.EventType)
        {
            case CorrespondenceDeleteEventType.HardDeletedByServiceOwner:
            case CorrespondenceDeleteEventType.HardDeletedByRecipient:
                if (ValidatePerformPurge(correspondence))
                {
                    await PurgeCorrespondence(correspondence, deletionEvent, cancellationToken);
                }
                break;
            case CorrespondenceDeleteEventType.SoftDeletedByRecipient:
            case CorrespondenceDeleteEventType.RestoredByRecipient:
                await SoftDeleteOrRestoreCorrespondence(correspondence, deletionEvent, enduserIdByPartyUuid, cancellationToken);
                break;
            default:
                logger.LogWarning("Unknown Deletion Event Type {EventType} for Correspondence {CorrespondenceId}. The event will be ignored.", deletionEvent.EventType, request.CorrespondenceId);
                break;
        }
    }

    private async Task<List<CorrespondenceDeleteEventEntity>> FilterDeleteEvents(SyncCorrespondenceStatusEventRequest request, CancellationToken cancellationToken)
    {
        if(request.SyncedDeleteEvents is null)
        {
            return new List<CorrespondenceDeleteEventEntity>();
        }

        var deletionEventsFilteredForRequestDuplicates = FilterDuplicateDeleteEvents(request.SyncedDeleteEvents);

        if (deletionEventsFilteredForRequestDuplicates.Count == 0)
        {
            return new List<CorrespondenceDeleteEventEntity>();
        }

        var deletionEventsToExecute = new List<CorrespondenceDeleteEventEntity>();
        var deletionEventsInDatabase = await correspondenceDeleteEventRepository.GetDeleteEventsForCorrespondenceId(request.CorrespondenceId, cancellationToken);
        
        foreach (var deletionEventToSync in deletionEventsFilteredForRequestDuplicates)
        {
            bool isDuplicate = deletionEventsInDatabase.Any(
                e => e.EventType == deletionEventToSync.EventType
                && e.EventOccurred.EqualsToSecond(deletionEventToSync.EventOccurred)
                && e.PartyUuid == deletionEventToSync.PartyUuid);

            if (isDuplicate)
            {
                logger.LogInformation("Current Deletion Event for {CorrespondenceId} has been deemed duplicate of existing and will be skipped. EventType: {EventType} - EventOccurred: {EventOccurred} - PartyUuid: {PartyUuid}", 
                    request.CorrespondenceId, deletionEventToSync.EventType, deletionEventToSync.EventOccurred, deletionEventToSync.PartyUuid);
            }
            else
            {
                deletionEventsToExecute.Add(deletionEventToSync);
            }
        }

        return deletionEventsToExecute;
    }

    private List<CorrespondenceStatusEntity> FilterStatusEvents(SyncCorrespondenceStatusEventRequest request, CorrespondenceEntity correspondence)
    {
        var eventsFilteredForCorrectStatus = new List<CorrespondenceStatusEntity>();

        if (request.SyncedEvents is null)
        {
            return eventsFilteredForCorrectStatus;
        }

        foreach (var statusEventToSync in request.SyncedEvents)
        {
            // Validate if the status event is valid for this handler / sync operation (unlikely, but possible)
            if (ValidateStatusUpdate(statusEventToSync))
            {
                eventsFilteredForCorrectStatus.Add(statusEventToSync);
            }
            else
            {
                logger.LogInformation(
                    "Status Event for {CorrespondenceId} has been deemed invalid and will be ignored. Status: {Status} - StatusChanged: {StatusChanged} - PartyUuid: {PartyUuid}",
                        request.CorrespondenceId, statusEventToSync.Status, statusEventToSync.StatusChanged, statusEventToSync.PartyUuid);
            }
        }
        
        if (eventsFilteredForCorrectStatus.Count == 0)
        {
            logger.LogWarning("None of the Status Events for {CorrespondenceId} has been deemed valid and no sync will be performed.", request.CorrespondenceId);
            return new List<CorrespondenceStatusEntity>();
        }

        // Remove possible duplicates from the request - This is because Altinn 2 uses two sets of data sources for status events, and we need to ensure that we only sync unique events.
        var eventsFilteredForRequestDuplicates = FilterDuplicateStatusEvents(eventsFilteredForCorrectStatus);

        var filteredStatusEvents = new List<CorrespondenceStatusEntity>();

        // Remove duplicate status events that are already present in the correspondence
        foreach (var syncedEvent in eventsFilteredForRequestDuplicates)
        {
            bool isDuplicate = correspondence.Statuses.Any(
                s => s.Status == syncedEvent.Status
                && s.StatusChanged.EqualsToSecond(syncedEvent.StatusChanged)
                && s.PartyUuid == syncedEvent.PartyUuid);

            if (isDuplicate)
            {
                logger.LogInformation("Current Status Event for {CorrespondenceId} has been deemed duplicate of existing and will be skipped. Status: {Status} - StatusChanged: {StatusChanged} - PartyUuid: {PartyUuid}", 
                    request.CorrespondenceId, syncedEvent.Status, syncedEvent.StatusChanged, syncedEvent.PartyUuid);
            }
            else
            {
                filteredStatusEvents.Add(syncedEvent);
            }
        }

        return filteredStatusEvents;
    }

    private async Task<Dictionary<Guid, string>> GetDialogPortenEndUserIdsForEvents(List<CorrespondenceStatusEntity>? statusEventsToExecute, List<CorrespondenceDeleteEventEntity>? deletionEventsToExecute, CancellationToken cancellationToken)
    {
        var enduserIdByPartyUuid = new Dictionary<Guid, string>();
        
        var partyUuidsToLookup = (statusEventsToExecute ?? Enumerable.Empty<CorrespondenceStatusEntity>())
            .Where(e => e.Status == CorrespondenceStatus.Archived) // Only Archived status events require Dialogporten enduserId
            .Select(e => e.PartyUuid)
            .Distinct();

        partyUuidsToLookup = partyUuidsToLookup
           .Concat((deletionEventsToExecute ?? Enumerable.Empty<CorrespondenceDeleteEventEntity>())
               .Where(e => e.EventType == CorrespondenceDeleteEventType.SoftDeletedByRecipient || e.EventType == CorrespondenceDeleteEventType.RestoredByRecipient) // Only SoftDelete and Restore events require Dialogporten enduserId
               .Select(e => e.PartyUuid)
               .Distinct()
           )
           .Where(uuid => !enduserIdByPartyUuid.ContainsKey(uuid));

        foreach (var uuid in partyUuidsToLookup)
        {
            var party = await altinnRegisterService.LookUpPartyByPartyUuid(uuid, cancellationToken)
                        ?? throw new ArgumentException($"Party with UUID {uuid} not found in Altinn Register.");
            if(party.PartyTypeName != PartyType.Person)
            {
                logger.LogWarning("Party with UUID {PartyUuid} has unsupported PartyType {PartyTypeName}. Cannot map to Dialogporten enduserId.", uuid, party.PartyTypeName);
            }
            else
            {
                enduserIdByPartyUuid[uuid] = $"{UrnConstants.PersonIdAttribute}:{party.SSN}";
            }   
        }

        return enduserIdByPartyUuid;
    }

    private static List<CorrespondenceStatusEntity> FilterDuplicateStatusEvents(List<CorrespondenceStatusEntity> input)
    {
        var exists = new HashSet<(CorrespondenceStatus Status, DateTimeOffset TruncatedStatusChanged, Guid PartyUuid)>();
        var result = new List<CorrespondenceStatusEntity>();

        foreach (var item in input)
        {
            var key = (
                item.Status,
                item.StatusChanged.TruncateToSecondUtc(),
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
                item.EventOccurred.TruncateToSecondUtc(),
                item.PartyUuid
            );

            if (exists.Add(key))
            {
                result.Add(item);
            }
        }
        return result;
    }

    /// <summary>
    /// Returns true if this status event type is allowed to be synced
    /// </summary>
    /// <param name="correspondence">The status event to validate.</param>
    /// <returns></returns>
    private bool ValidateStatusUpdate(CorrespondenceStatusEntity statusEntity)
    {
        var validStatuses = new[] { CorrespondenceStatus.Read, CorrespondenceStatus.Confirmed, CorrespondenceStatus.Archived };

        if (!validStatuses.Contains(statusEntity.Status))
        {
            return false;
        }

        return true;
    }

    private bool ValidatePerformPurge(CorrespondenceEntity correspondence)
    {
        if( correspondence.StatusHasBeen(CorrespondenceStatus.PurgedByAltinn) || correspondence.StatusHasBeen(CorrespondenceStatus.PurgedByRecipient))
        {
            logger.LogWarning("Correspondence {CorrespondenceId} has already been purged - cannot purge again", correspondence.Id);
            return false;
        }
        return true;
    }

    public async Task<Guid> PurgeCorrespondence(CorrespondenceEntity correspondence, CorrespondenceDeleteEventEntity deleteEventToSync, CancellationToken cancellationToken)
    {
        var corrStatus = CorrespondenceStatus.PurgedByRecipient;
        DateTimeOffset syncedTimestamp = DateTimeOffset.UtcNow;
        switch (deleteEventToSync.EventType)
        {
            case CorrespondenceDeleteEventType.HardDeletedByServiceOwner:
                corrStatus = CorrespondenceStatus.PurgedByAltinn;
                break;
            case CorrespondenceDeleteEventType.HardDeletedByRecipient:
                corrStatus = CorrespondenceStatus.PurgedByRecipient;
                break;
            default:
                throw new ArgumentException($"Cannot perform PurgeCorrespondence for {deleteEventToSync.EventType}");
        }

        // Save to Correspondence Database
        await StoreDeleteEventAsCorrespondenceStatus(correspondence, corrStatus, deleteEventToSync, syncedTimestamp, cancellationToken);
        await StoreDeleteEventForCorrespondence(correspondence, deleteEventToSync, syncedTimestamp, cancellationToken);

        if (correspondence.IsMigrating == false)
        {
            backgroundJobClient.Enqueue<IEventBus>((eventBus) => eventBus.Publish(AltinnEventType.CorrespondencePurged, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, CancellationToken.None));
        }

        await purgeCorrespondenceHelper.CheckAndPurgeAttachments(correspondence.Id, deleteEventToSync.PartyUuid, cancellationToken);

        if (correspondence.IsMigrating == false)
        {
            var actorType = deleteEventToSync.EventType == CorrespondenceDeleteEventType.HardDeletedByServiceOwner ? DialogportenActorType.Sender : DialogportenActorType.Recipient;
            var actorName = deleteEventToSync.EventType == CorrespondenceDeleteEventType.HardDeletedByServiceOwner ? "avsender" : "mottaker";
            var purgedActivityJob = backgroundJobClient.Enqueue<IDialogportenService>(service => service.CreateCorrespondencePurgedActivity(correspondence.Id, actorType, actorName, deleteEventToSync.EventOccurred));

            var dialogId = correspondence.ExternalReferences.FirstOrDefault(reference => reference.ReferenceType == ReferenceType.DialogportenDialogId)?.ReferenceValue;
            if (dialogId is null)
            {   
                throw new ArgumentException($"No dialog found on correspondence with id {correspondence.Id}");
            }
            backgroundJobClient.ContinueJobWith<IDialogportenService>(purgedActivityJob, service => service.SoftDeleteDialog(dialogId));
        }

        return correspondence.Id;
    }

    private async Task SoftDeleteOrRestoreCorrespondence(CorrespondenceEntity correspondence, CorrespondenceDeleteEventEntity deleteEventToSync, Dictionary<Guid, string> enduserIdByPartyUuid, CancellationToken cancellationToken)
    {
        DateTimeOffset syncedTimestamp = DateTimeOffset.UtcNow;
        if (CorrespondenceDeleteEventType.SoftDeletedByRecipient != deleteEventToSync.EventType && CorrespondenceDeleteEventType.RestoredByRecipient != deleteEventToSync.EventType)
        {
            throw new ArgumentException($"Cannot perform SoftDeleteOrRestoreCorrespondence for {deleteEventToSync.EventType}");
        }

        // Save to Correspondence Database, no CorrrespondenceStatus for soft delete / restore
        await StoreDeleteEventForCorrespondence(correspondence, deleteEventToSync, syncedTimestamp, cancellationToken);

        if (correspondence.IsMigrating == false)
        {
            if (correspondence.StatusHasBeen(CorrespondenceStatus.PurgedByAltinn) || correspondence.StatusHasBeen(CorrespondenceStatus.PurgedByRecipient))
            {
                logger.LogWarning("Skipping updating dialog for {EventType} for Purged correspondence {CorrespondenceId} at {EventOccurred}.", deleteEventToSync.EventType, correspondence.Id, deleteEventToSync.EventOccurred);
            }
            else if (!enduserIdByPartyUuid.ContainsKey(deleteEventToSync.PartyUuid))
            {
                logger.LogWarning("Skipping updating dialog for {EventType} for correspondence {CorrespondenceId} at {EventOccurred} due to missing Dialogporten enduserId for party {PartyUuid}.", deleteEventToSync.EventType, correspondence.Id, deleteEventToSync.EventOccurred, deleteEventToSync.PartyUuid);
            }
            else
            {
                // Perform SoftDelete or Restore in Dialogporten
                bool isArchived = correspondence.StatusHasBeen(CorrespondenceStatus.Archived);
                await SetSoftDeleteOrRestoreOnDialog(correspondence.Id, enduserIdByPartyUuid[deleteEventToSync.PartyUuid], deleteEventToSync.EventType, isArchived, cancellationToken);
            }
        }
    }

    private async Task StoreStatusEventAsCorrespondenceStatus(CorrespondenceEntity correspondence, CorrespondenceStatusEntity statusEventToSync, DateTimeOffset syncedTimestamp, CancellationToken cancellationToken)
    {
        CorrespondenceStatusEntity statusToSave = new CorrespondenceStatusEntity()
        {
            CorrespondenceId = correspondence.Id,
            StatusText = $"Synced event {statusEventToSync.Status} from Altinn 2",
            Status = statusEventToSync.Status,
            StatusChanged = statusEventToSync.StatusChanged,
            PartyUuid = statusEventToSync.PartyUuid,
            SyncedFromAltinn2 = syncedTimestamp
        };
        await correspondenceStatusRepository.AddCorrespondenceStatus(statusToSave, cancellationToken);
    }

    private async Task StoreDeleteEventAsCorrespondenceStatus(CorrespondenceEntity correspondence, CorrespondenceStatus statusCodeToSave, CorrespondenceDeleteEventEntity deleteEventToSync, DateTimeOffset syncedTimestamp, CancellationToken cancellationToken)
    {
        CorrespondenceStatusEntity statusToSave = new CorrespondenceStatusEntity()
        {
            CorrespondenceId = correspondence.Id,
            Status = statusCodeToSave,
            StatusChanged = deleteEventToSync.EventOccurred,
            StatusText = $"Synced event {statusCodeToSave} from Altinn 2",
            PartyUuid = deleteEventToSync.PartyUuid,
            SyncedFromAltinn2 = syncedTimestamp
        };
        await correspondenceStatusRepository.AddCorrespondenceStatus(statusToSave, cancellationToken);
    }

    private async Task StoreDeleteEventForCorrespondence(CorrespondenceEntity correspondence, CorrespondenceDeleteEventEntity deleteEventToSync, DateTimeOffset syncedTimestamp, CancellationToken cancellationToken)
    {
        deleteEventToSync.CorrespondenceId = correspondence.Id;
        deleteEventToSync.SyncedFromAltinn2 = syncedTimestamp;
        await correspondenceDeleteEventRepository.AddDeleteEvent(deleteEventToSync, cancellationToken);
    }

    private async Task SetSoftDeleteOrRestoreOnDialog(Guid correspondenceId, string endUserId, CorrespondenceDeleteEventType eventType, bool isArchived, CancellationToken cancellationToken)
    {
        switch (eventType)
        {
            case CorrespondenceDeleteEventType.SoftDeletedByRecipient:
                {
                    backgroundJobClient.Enqueue<IDialogportenService>(service => service.UpdateSystemLabelsOnDialog(correspondenceId, endUserId, new List<DialogPortenSystemLabel> { DialogPortenSystemLabel.Bin }, null));
                    break;
                }

            case CorrespondenceDeleteEventType.RestoredByRecipient:
                {
                    if (isArchived)
                    {
                        // Add "Archive" label if the correspondence has been archived
                        backgroundJobClient.Enqueue<IDialogportenService>(service => service.UpdateSystemLabelsOnDialog(correspondenceId, endUserId, new List<DialogPortenSystemLabel> { DialogPortenSystemLabel.Archive }, null));
                    }
                    else
                    {
                        backgroundJobClient.Enqueue<IDialogportenService>(service => service.UpdateSystemLabelsOnDialog(correspondenceId, endUserId, null, new List<DialogPortenSystemLabel> { DialogPortenSystemLabel.Bin }));
                    }
                    break;
                }

            default:
                throw new ArgumentException($"Cannot perform ChangeSoftDeleteLabelInDialogPorten for correspondence {correspondenceId} with event type {eventType}");
        }
    }

}
