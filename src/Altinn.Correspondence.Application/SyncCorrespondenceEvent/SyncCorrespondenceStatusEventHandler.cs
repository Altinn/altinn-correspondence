using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.PurgeCorrespondence;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Altinn.Correspondence.Integrations.Altinn.Register;
using Altinn.Correspondence.Persistence.Repositories;
using Hangfire;
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
        
        var statusEventsToExecute = new List<CorrespondenceStatusEntity>();
        var deletionEventsToExecute = new List<CorrespondenceDeleteEventEntity>();

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
                    if (ValidateStatusUpdate(statusEventToSync))
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
            }

            // Remove possible duplicates from the request - This is because Altinn 2 uses two sets of data sources for status events, and we need to ensure that we only sync unique events.
            var eventsFilteredForRequestDuplicates = FilterDuplicateStatusEvents(eventsFilteredForCorrectStatus);

            // Remove duplicate status events that are already present in the correspondence
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
                    statusEventsToExecute.Add(syncedEvent);
                }
            }
            if (statusEventsToExecute.Count == 0)
            {
                logger.LogWarning("None of the Status Events for {CorrespondenceId} were unique, and no sync will be performed.", request.CorrespondenceId);
            }
        }

        if (numSyncedDeletes > 0)
        {
            var deletionEventsFilteredForRequestDuplicates = FilterDuplicateDeleteEvents(request.SyncedDeleteEvents);

            var deletionEventsInDatabase = await correspondenceDeleteEventRepository.GetDeleteEventsForCorrespondenceId(request.CorrespondenceId, cancellationToken);            
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
                    deletionEventsToExecute.Add(deletionEventToSync);
                }
            }

            // Sort by EventOccurred ascending
            deletionEventsToExecute = deletionEventsToExecute.OrderBy(e => e.EventOccurred).ToList();
        }

        var txResult = await TransactionWithRetriesPolicy.Execute<Guid>(async (cancellationToken) =>
        {
            if (statusEventsToExecute.Count > 0)
            {
                logger.LogInformation("Executing status event sync transaction for correspondence {CorrespondenceId} with {SyncedEventsCount} # of status events", request.CorrespondenceId, statusEventsToExecute.Count);
                // Log the status events to the database
                foreach (var entity in statusEventsToExecute)
                {
                    entity.CorrespondenceId = correspondence.Id;
                    entity.SyncedFromAltinn2 = DateTimeOffset.UtcNow;
                    entity.StatusText = $"Synced event {entity.Status} from Altinn 2";
                }
                await correspondenceStatusRepository.AddCorrespondenceStatuses(statusEventsToExecute, cancellationToken);

                // Events and updates to Dialogporten and EventBus are only relevant for migrated correspondences that have been fully migrated (IsMigrating = false)
                if (correspondence.IsMigrating == false)
                {
                    foreach (var eventToExecute in statusEventsToExecute)
                    {
                        logger.LogDebug("Perform Sync status event {Status} for {CorrespondenceId}", eventToExecute.Status, request.CorrespondenceId);
                        switch (eventToExecute.Status)
                        {   
                            case CorrespondenceStatus.Confirmed:
                                {
                                    backgroundJobClient.Enqueue<IDialogportenService>((dialogportenService) => dialogportenService.CreateConfirmedActivity(request.CorrespondenceId, DialogportenActorType.Recipient, eventToExecute.StatusChanged)); // Set the operationtime to the time the status was changed in Altinn 2;
                                    backgroundJobClient.Enqueue<IDialogportenService>((dialogportenService) => dialogportenService.PatchCorrespondenceDialogToConfirmed(request.CorrespondenceId));
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
                                    var endUserParty = await altinnRegisterService.LookUpPartyByPartyUuid(eventToExecute.PartyUuid, cancellationToken) ?? throw new ArgumentException($"Party with UUID {eventToExecute.PartyUuid} not found in Altinn Register - cannot set archived Systemlabel for correspondence {request.CorrespondenceId}.");
                                    backgroundJobClient.Enqueue<IDialogportenService>(service => service.UpdateSystemLabelsOnDialog(request.CorrespondenceId, GetPrefixedIdentifierForParty(endUserParty), new List<string> { "Archive" }, null)); ;
                                    break;
                                }
                            default:
                                logger.LogWarning("Unsupported Status Event type {Status} for Correspondence {CorrespondenceId}. The event will be ignored.", eventToExecute.Status, request.CorrespondenceId);
                                break;
                        }
                    }
                }
            }

            // Handle deletion events
            if (deletionEventsToExecute.Count > 0)
            {
                logger.LogInformation("Executing delete event sync transaction for correspondence {CorrespondenceId} with {SyncedEventsCount} # of delete events", request.CorrespondenceId, deletionEventsToExecute.Count);
                foreach (var deletionEvent in deletionEventsToExecute)
                {
                    logger.LogDebug("Perform sync of delete event {EventType} for {CorrespondenceId}", deletionEvent.EventType, request.CorrespondenceId);
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
                            await SoftDeleteOrRestoreCorrespondence(correspondence, deletionEvent, cancellationToken);
                            break;
                        default:
                            logger.LogWarning("Unknown Deletion Event Type {EventType} for Correspondence {CorrespondenceId}. The event will be ignored.", deletionEvent.EventType, request.CorrespondenceId);
                            break;
                    }
                }
            }
            
            return request.CorrespondenceId;
        }, logger, cancellationToken);

        txResult.Switch(
        _ => logger.LogInformation("Successfully synced request for correspondence {CorrespondenceId} with {numSyncedEvents} # status events and {numSyncedDeletes} # delete events", request.CorrespondenceId, numSyncedEvents, numSyncedDeletes),
            err => logger.LogWarning("Failed to sync request for correspondence {CorrespondenceId}: {Error}", request.CorrespondenceId, err));
        return txResult;
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

    /// <summary>
    /// Validates if the current status of the correspondence should be synced
    /// </summary>
    /// <param name="correspondence">The correspondence entity to validate</param>
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

        await correspondenceStatusRepository.AddCorrespondenceStatus(new CorrespondenceStatusEntity()
        {
            CorrespondenceId = correspondence.Id,
            Status = corrStatus,
            StatusChanged = deleteEventToSync.EventOccurred,
            StatusText = $"Synced event {corrStatus} from Altinn 2",
            PartyUuid = deleteEventToSync.PartyUuid,
            SyncedFromAltinn2 = syncedTimestamp
        }, cancellationToken);

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

    private async Task SoftDeleteOrRestoreCorrespondence(CorrespondenceEntity correspondence, CorrespondenceDeleteEventEntity deleteEventToSync, CancellationToken cancellationToken)
    {
        DateTimeOffset syncedTimestamp = DateTimeOffset.UtcNow;
        if (CorrespondenceDeleteEventType.SoftDeletedByRecipient != deleteEventToSync.EventType && CorrespondenceDeleteEventType.RestoredByRecipient != deleteEventToSync.EventType)
        {
            throw new ArgumentException($"Cannot perform SoftDeleteOrRestoreCorrespondence for {deleteEventToSync.EventType}");
        }

        await StoreDeleteEventForCorrespondence(correspondence, deleteEventToSync, syncedTimestamp, cancellationToken);

        if (correspondence.IsMigrating == false)
        {
            await SetSoftDeleteOrRestoreOnDialog(correspondence.Id, deleteEventToSync.PartyUuid, deleteEventToSync.EventType, cancellationToken);
        }
    }

    private async Task StoreDeleteEventForCorrespondence(CorrespondenceEntity correspondence, CorrespondenceDeleteEventEntity deleteEventToSync, DateTimeOffset syncedTimestamp, CancellationToken cancellationToken)
    {
        deleteEventToSync.CorrespondenceId = correspondence.Id;
        deleteEventToSync.SyncedFromAltinn2 = syncedTimestamp;
        await correspondenceDeleteEventRepository.AddDeleteEvent(deleteEventToSync, cancellationToken);
    }

    private async Task SetSoftDeleteOrRestoreOnDialog(Guid correspondenceId, Guid partyUuid, CorrespondenceDeleteEventType eventType, CancellationToken cancellationToken)
    {
        var endUserParty = await altinnRegisterService.LookUpPartyByPartyUuid(partyUuid, cancellationToken);
        if (endUserParty is null)
        {
            throw new ArgumentException($"Party with UUID {partyUuid} not found in Altinn Register - cannot Report Systemlabel for correspondence {correspondenceId}.");
        }

        switch (eventType)
        {
            case CorrespondenceDeleteEventType.SoftDeletedByRecipient:
                {
                    backgroundJobClient.Enqueue<IDialogportenService>(service => service.UpdateSystemLabelsOnDialog(correspondenceId, GetPrefixedIdentifierForParty(endUserParty), new List<string> { "Bin" }, null));
                    break;
                }

            case CorrespondenceDeleteEventType.RestoredByRecipient:
                {
                    backgroundJobClient.Enqueue<IDialogportenService>(service => service.UpdateSystemLabelsOnDialog(correspondenceId, GetPrefixedIdentifierForParty(endUserParty), null, new List<string> { "Bin" }));
                    break;
                }

            default:
                throw new ArgumentException($"Cannot perform ChangeSoftDeleteLabelInDialogPorten for correspondence {correspondenceId} with event type {eventType}");
        }
    }

    private string GetPrefixedIdentifierForParty(Party party)
    {
        if (party.PartyTypeName == PartyType.Organization)
        {
            return $"{UrnConstants.OrganizationNumberAttribute}:{party.OrgNumber}";
        }
        else if (party.PartyTypeName == PartyType.Person)
        {
            return $"{UrnConstants.PersonIdAttribute}:{party.SSN}";
        }
        else
        {
            throw new ArgumentException($"Unsupported party type: {party.PartyTypeName}");
        }
    }
}
