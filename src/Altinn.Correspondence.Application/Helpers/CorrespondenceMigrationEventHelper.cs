using Altinn.Correspondence.Application.PurgeCorrespondence;
using Altinn.Correspondence.Application.SyncCorrespondenceEvent;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Application.Helpers;

/// <summary>
/// Helper to enable both sync of various types of events from Altinn 2, as well as enable re-migration of already migrated correspondences by re-processing events for a given correspondence.
/// </summary>
public class CorrespondenceMigrationEventHelper(
    ICorrespondenceStatusRepository correspondenceStatusRepository,
    ICorrespondenceDeleteEventRepository correspondenceDeleteEventRepository,
    ICorrespondenceNotificationRepository correspondenceNotificationRepository,
    ICorrespondenceForwardingEventRepository correspondenceForwardingEventRepository,
    IAltinnRegisterService altinnRegisterService,
    PurgeCorrespondenceHelper purgeCorrespondenceHelper,
    IBackgroundJobClient backgroundJobClient,
    ILogger<CorrespondenceMigrationEventHelper> logger)
{
    public async Task ProcessStatusEvent(Guid correspondenceId, CorrespondenceEntity correspondence, Dictionary<Guid, string> enduserIdByPartyUuid, CorrespondenceStatusEntity eventToExecute, CancellationToken cancellationToken)
    {
        logger.LogDebug("Process Sync status event {Status} for {CorrespondenceId}", eventToExecute.Status, correspondenceId);
        
        // Enqueue background jobs inside the transaction
        if (correspondence.IsMigrating == false)
        {
            switch (eventToExecute.Status)
            {
                case CorrespondenceStatus.Confirmed:
                    {
                        var patchJobId = backgroundJobClient.Enqueue<IDialogportenService>((dialogportenService) => dialogportenService.PatchCorrespondenceDialogToConfirmed(correspondenceId, CancellationToken.None));
                        if (!enduserIdByPartyUuid.TryGetValue(eventToExecute.PartyUuid, out var endUserId))
                        {
                            logger.LogWarning("Skipping updating dialog for Confirm for correspondence {CorrespondenceId} at {StatusChanged} due to missing Dialogporten enduserId for party {PartyUuid}.", correspondence.Id, eventToExecute.StatusChanged, eventToExecute.PartyUuid);
                        }
                        else
                        {
                            backgroundJobClient.ContinueJobWith<IDialogportenService>(patchJobId, (dialogportenService) => dialogportenService.CreateConfirmedActivity(correspondenceId, DialogportenActorType.Recipient, eventToExecute.StatusChanged, endUserId), JobContinuationOptions.OnlyOnSucceededState); // Set the operationtime to the time the status was changed in Altinn 2
                        }                        
                        backgroundJobClient.Enqueue<IEventBus>((eventBus) => eventBus.Publish(AltinnEventType.CorrespondenceReceiverConfirmed, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, CancellationToken.None));
                        break;
                    }

                case CorrespondenceStatus.Read:
                    {
                        
                        if (!enduserIdByPartyUuid.TryGetValue(eventToExecute.PartyUuid, out var endUserId))
                        {
                            logger.LogWarning("Skipping updating dialog for Read for correspondence {CorrespondenceId} at {StatusChanged} due to missing Dialogporten enduserId for party {PartyUuid}.", correspondence.Id, eventToExecute.StatusChanged, eventToExecute.PartyUuid);
                        }
                        else
                        {
                            backgroundJobClient.Enqueue<IDialogportenService>((dialogportenService) => dialogportenService.CreateOpenedActivity(correspondence.Id, DialogportenActorType.Recipient, eventToExecute.StatusChanged, endUserId));
                        }
                        backgroundJobClient.Enqueue<IEventBus>((eventBus) => eventBus.Publish(AltinnEventType.CorrespondenceReceiverRead, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, CancellationToken.None));
                        break;
                    }

                case CorrespondenceStatus.Archived:
                    {
                        if (!enduserIdByPartyUuid.TryGetValue(eventToExecute.PartyUuid, out var endUserId))
                        {
                            logger.LogWarning("Skipping updating dialog for Archived for correspondence {CorrespondenceId} at {StatusChanged} due to missing Dialogporten enduserId for party {PartyUuid}.", correspondence.Id, eventToExecute.StatusChanged, eventToExecute.PartyUuid);
                        }
                        else
                        {
                            backgroundJobClient.Enqueue<IDialogportenService>(service => service.UpdateSystemLabelsOnDialog(correspondence.Id, endUserId, DialogportenActorType.PartyRepresentative, new List<DialogPortenSystemLabel> { DialogPortenSystemLabel.Archive }, null));
                        }                        
                        break;
                    }
                default:
                    logger.LogWarning("Unsupported Status Event type {Status} for Correspondence {CorrespondenceId} at {StatusChanged}. The event will be ignored.", eventToExecute.Status, correspondenceId, eventToExecute.StatusChanged);
                    break;
            }
        }

        // Save status to Correspondence Database - this is the critical database operation that must succeed within the transaction
        await StoreStatusEventAsCorrespondenceStatus(correspondence, eventToExecute, DateTimeOffset.UtcNow, cancellationToken);
    }

    public async Task ProcessDeleteEvent(Guid correspondenceId, CorrespondenceEntity correspondence, Dictionary<Guid, string> enduserIdByPartyUuid, CorrespondenceDeleteEventEntity deletionEvent, CancellationToken cancellationToken)
    {
        logger.LogDebug("Process Sync delete event {EventType} for {CorrespondenceId}", deletionEvent.EventType, correspondenceId);
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
                logger.LogWarning("Unknown Deletion Event Type {EventType} for Correspondence {CorrespondenceId}. The event will be ignored.", deletionEvent.EventType, correspondenceId);
                break;
        }
    }

    public async Task<List<CorrespondenceDeleteEventEntity>> FilterDeleteEvents(Guid correspondenceId, List<CorrespondenceDeleteEventEntity>? syncedDeleteEvents, CancellationToken cancellationToken)
    {
        if(syncedDeleteEvents is null)
        {
            return new List<CorrespondenceDeleteEventEntity>();
        }

        var deletionEventsFilteredForRequestDuplicates = FilterDuplicateDeleteEvents(syncedDeleteEvents);

        if (deletionEventsFilteredForRequestDuplicates.Count == 0)
        {
            return new List<CorrespondenceDeleteEventEntity>();
        }

        var deletionEventsToExecute = new List<CorrespondenceDeleteEventEntity>();
        var deletionEventsInDatabase = await correspondenceDeleteEventRepository.GetDeleteEventsForCorrespondenceId(correspondenceId, cancellationToken);
        
        foreach (var deletionEventToSync in deletionEventsFilteredForRequestDuplicates)
        {
            bool isDuplicate = deletionEventsInDatabase.Any(
                e => e.EventType == deletionEventToSync.EventType
                && e.EventOccurred.EqualsToSecond(deletionEventToSync.EventOccurred)
                && e.PartyUuid == deletionEventToSync.PartyUuid);

            if (isDuplicate)
            {
                logger.LogInformation("Current Deletion Event for {CorrespondenceId} has been deemed duplicate of existing and will be skipped. EventType: {EventType} - EventOccurred: {EventOccurred} - PartyUuid: {PartyUuid}", 
                    correspondenceId, deletionEventToSync.EventType, deletionEventToSync.EventOccurred, deletionEventToSync.PartyUuid);
            }
            else
            {
                deletionEventsToExecute.Add(deletionEventToSync);
            }
        }

        return deletionEventsToExecute;
    }

    public List<CorrespondenceStatusEntity> FilterStatusEvents(Guid correspondenceId, List<CorrespondenceStatusEntity>? syncedEvents, CorrespondenceEntity correspondence)
    {
        var eventsFilteredForCorrectStatus = new List<CorrespondenceStatusEntity>();

        if (syncedEvents is null)
        {
            return eventsFilteredForCorrectStatus;
        }

        foreach (var statusEventToSync in syncedEvents)
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
                        correspondenceId, statusEventToSync.Status, statusEventToSync.StatusChanged, statusEventToSync.PartyUuid);
            }
        }
        
        if (eventsFilteredForCorrectStatus.Count == 0)
        {
            logger.LogWarning("None of the Status Events for {CorrespondenceId} has been deemed valid and no sync will be performed.", correspondenceId);
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
                    correspondenceId, syncedEvent.Status, syncedEvent.StatusChanged, syncedEvent.PartyUuid);
            }
            else
            {
                filteredStatusEvents.Add(syncedEvent);
            }
        }

        return filteredStatusEvents;
    }

    public async Task<Dictionary<Guid, string>> GetDialogPortenEndUserIdsForEvents(List<CorrespondenceStatusEntity>? statusEventsToExecute, List<CorrespondenceDeleteEventEntity>? deletionEventsToExecute, CancellationToken cancellationToken)
    {
        var enduserIdByPartyUuid = new Dictionary<Guid, string>();
        
        var partyUuidsToLookup = (statusEventsToExecute ?? Enumerable.Empty<CorrespondenceStatusEntity>())
            .Where(e => e.Status == CorrespondenceStatus.Read || e.Status == CorrespondenceStatus.Confirmed || e.Status == CorrespondenceStatus.Archived) // Only Read, Confirm, Archived status events require Dialogporten enduserId/urn
            .Select(e => e.PartyUuid)
            .Distinct();

        partyUuidsToLookup = partyUuidsToLookup
           .Concat((deletionEventsToExecute ?? Enumerable.Empty<CorrespondenceDeleteEventEntity>())
               .Where(e => e.EventType == CorrespondenceDeleteEventType.SoftDeletedByRecipient || e.EventType == CorrespondenceDeleteEventType.RestoredByRecipient) // Only SoftDelete and Restore events require Dialogporten enduserId
               .Select(e => e.PartyUuid))
           .Distinct(); // Handles all duplicates
        
        foreach (var uuid in partyUuidsToLookup)
        {
            var party = await altinnRegisterService.LookUpPartyByPartyUuid(uuid, cancellationToken);
            if (party is null)
            {
                logger.LogWarning("Party with UUID {PartyUuid} not found in Altinn Register. Skipping Dialogporten mapping, which may lead to issues later on.", uuid);
                continue;
            }
            switch (party.PartyTypeName)
            {
                case PartyType.Person:
                    enduserIdByPartyUuid[uuid] = $"{UrnConstants.PersonIdAttribute}:{party.SSN}";
                    break;
                case PartyType.Organization:
                    enduserIdByPartyUuid[uuid] = $"{UrnConstants.OrganizationNumberAttribute}:{party.OrgNumber}";
                    break;
                default:
                    logger.LogWarning("Party with UUID {PartyUuid} has unsupported PartyType {PartyTypeName}. Cannot map to Dialogporten enduserId.", uuid, party.PartyTypeName);
                    break;
            }
        }

        return enduserIdByPartyUuid;
    }

    public static List<CorrespondenceStatusEntity> FilterDuplicateStatusEvents(List<CorrespondenceStatusEntity> input)
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

    public static List<CorrespondenceDeleteEventEntity> FilterDuplicateDeleteEvents(List<CorrespondenceDeleteEventEntity> input)
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
    /// <param name="statusEntity">The status event to validate.</param>
    /// <returns></returns>
    public bool ValidateStatusUpdate(CorrespondenceStatusEntity statusEntity)
    {
        var validStatuses = new[] { CorrespondenceStatus.Read, CorrespondenceStatus.Confirmed, CorrespondenceStatus.Archived };

        if (!validStatuses.Contains(statusEntity.Status))
        {
            return false;
        }

        return true;
    }

    public bool ValidatePerformPurge(CorrespondenceEntity correspondence)
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
            var purgedActivityJobId = backgroundJobClient.Enqueue<IDialogportenService>(service => service.CreateCorrespondencePurgedActivity(correspondence.Id, actorType, actorName, deleteEventToSync.EventOccurred));

            var dialogId = correspondence.ExternalReferences.FirstOrDefault(reference => reference.ReferenceType == ReferenceType.DialogportenDialogId)?.ReferenceValue;
            if (dialogId is null)
            {   
                throw new ArgumentException($"No dialog found on correspondence with id {correspondence.Id}");
            }
            backgroundJobClient.ContinueJobWith<IDialogportenService>(purgedActivityJobId, service => service.SoftDeleteDialog(dialogId), JobContinuationOptions.OnlyOnSucceededState);
        }

        return correspondence.Id;
    }

    public async Task SoftDeleteOrRestoreCorrespondence(CorrespondenceEntity correspondence, CorrespondenceDeleteEventEntity deleteEventToSync, Dictionary<Guid, string> enduserIdByPartyUuid, CancellationToken cancellationToken)
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
                logger.LogWarning("Skipping updating dialog for {EventType} for correspondence {CorrespondenceId} at {EventOccurred} due to the Correspondence being purged.", deleteEventToSync.EventType, correspondence.Id, deleteEventToSync.EventOccurred);
            }
            else if (!enduserIdByPartyUuid.TryGetValue(deleteEventToSync.PartyUuid, out var endUserId))
            {
                logger.LogWarning("Skipping updating dialog for {EventType} for correspondence {CorrespondenceId} at {EventOccurred} due to missing Dialogporten enduserId for party {PartyUuid}.", deleteEventToSync.EventType, correspondence.Id, deleteEventToSync.EventOccurred, deleteEventToSync.PartyUuid);
            }
            else
            {
                // Enqueue SoftDelete or Restore in Dialogporten
                bool isArchived = correspondence.StatusHasBeen(CorrespondenceStatus.Archived);
                SetSoftDeleteOrRestoreOnDialog(correspondence.Id, endUserId, deleteEventToSync.EventType, isArchived);
            }
        }
    }

    public async Task StoreStatusEventAsCorrespondenceStatus(CorrespondenceEntity correspondence, CorrespondenceStatusEntity statusEventToSync, DateTimeOffset syncedTimestamp, CancellationToken cancellationToken)
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

    public async Task StoreDeleteEventAsCorrespondenceStatus(CorrespondenceEntity correspondence, CorrespondenceStatus statusCodeToSave, CorrespondenceDeleteEventEntity deleteEventToSync, DateTimeOffset syncedTimestamp, CancellationToken cancellationToken)
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

    public async Task StoreDeleteEventForCorrespondence(CorrespondenceEntity correspondence, CorrespondenceDeleteEventEntity deleteEventToSync, DateTimeOffset syncedTimestamp, CancellationToken cancellationToken)
    {
        deleteEventToSync.CorrespondenceId = correspondence.Id;
        deleteEventToSync.Correspondence = null; // Clear navigation property to prevent EF Core from tracking the correspondence entity
        deleteEventToSync.SyncedFromAltinn2 = syncedTimestamp;
        await correspondenceDeleteEventRepository.AddDeleteEvent(deleteEventToSync, cancellationToken);
    }

    public void SetSoftDeleteOrRestoreOnDialog(Guid correspondenceId, string endUserId, CorrespondenceDeleteEventType eventType, bool isArchived)
    {
        switch (eventType)
        {
            case CorrespondenceDeleteEventType.SoftDeletedByRecipient:
                {
                    backgroundJobClient.Enqueue<IDialogportenService>(service => service.UpdateSystemLabelsOnDialog(correspondenceId, endUserId, DialogportenActorType.PartyRepresentative, new List<DialogPortenSystemLabel> { DialogPortenSystemLabel.Bin }, null));
                    break;
                }

            case CorrespondenceDeleteEventType.RestoredByRecipient:
                {
                    if (isArchived)
                    {
                        // Add "Archive" label if the correspondence has been archived
                        backgroundJobClient.Enqueue<IDialogportenService>(service => service.UpdateSystemLabelsOnDialog(correspondenceId, endUserId, DialogportenActorType.PartyRepresentative, new List<DialogPortenSystemLabel> { DialogPortenSystemLabel.Archive }, null));
                    }
                    else
                    {
                        backgroundJobClient.Enqueue<IDialogportenService>(service => service.UpdateSystemLabelsOnDialog(correspondenceId, endUserId, DialogportenActorType.PartyRepresentative, null, new List<DialogPortenSystemLabel> { DialogPortenSystemLabel.Bin }));
                    }
                    break;
                }

            default:
                throw new ArgumentException($"Cannot perform ChangeSoftDeleteLabelInDialogPorten for correspondence {correspondenceId} with event type {eventType}");
        }
    }

    public async Task<bool> IsCorrespondenceSoftDeleted(CorrespondenceEntity correspondence, CancellationToken cancellationToken)
    {
        var deletionEventsInDatabase = await correspondenceDeleteEventRepository.GetDeleteEventsForCorrespondenceId(correspondence.Id, cancellationToken);
        if (deletionEventsInDatabase == null || !deletionEventsInDatabase.Any())
        {
            return false;
        }

        var softDeletedEvent = deletionEventsInDatabase
            .Where(e => e.EventType == CorrespondenceDeleteEventType.SoftDeletedByRecipient)
            .OrderByDescending(e => e.EventOccurred)
            .FirstOrDefault();

        var restoredEvent = deletionEventsInDatabase
            .Where(e => e.EventType == CorrespondenceDeleteEventType.RestoredByRecipient)
            .OrderByDescending(e => e.EventOccurred)
            .FirstOrDefault();

        // If no soft delete event exists, it's not soft deleted
        if (softDeletedEvent == null)
        {
            return false;
        }

        // If no restore event exists, it is still soft deleted
        if (restoredEvent == null)
        {
            return true;
        }

        // Return true if soft delete occurred after the most recent restore
        return softDeletedEvent.EventOccurred > restoredEvent.EventOccurred;
    }

    public List<CorrespondenceNotificationEntity> FilterNotificationEvents(Guid correspondenceId, List<CorrespondenceNotificationEntity>? syncedEvents, CorrespondenceEntity correspondence)
    {
        var notificationsToProcess = new List<CorrespondenceNotificationEntity>();

        if (syncedEvents is null)
        {
            return notificationsToProcess;
        }

        foreach (var syncedEvent in syncedEvents)
        {
            bool isDuplicate = (correspondence.Notifications ?? Enumerable.Empty<CorrespondenceNotificationEntity>()).Any(
                n => n.NotificationAddress == syncedEvent.NotificationAddress
                && n.NotificationChannel == syncedEvent.NotificationChannel
                && n.NotificationSent.HasValue
                && syncedEvent.NotificationSent.HasValue
                && n.NotificationSent.Value.EqualsToSecond(syncedEvent.NotificationSent.Value)
                && n.Altinn2NotificationId == syncedEvent.Altinn2NotificationId);

            if (isDuplicate)
            {
                logger.LogInformation("Current Notification Event for {CorrespondenceId} has been deemed duplicate of existing and will be skipped. NotificationId: {NotificationId} - NotificationSent: {NotificationSent}",
                    correspondenceId, syncedEvent.Altinn2NotificationId, syncedEvent.NotificationSent);
            }
            else
            {
                notificationsToProcess.Add(syncedEvent);
            }
        }

        return notificationsToProcess;
    }

    public List<CorrespondenceForwardingEventEntity> FilterForwardingEvents(Guid correspondenceId, List<CorrespondenceForwardingEventEntity>? syncedEvents, CorrespondenceEntity correspondence)
    {
        var forwardingEventsToProcess = new List<CorrespondenceForwardingEventEntity>();

        if (syncedEvents is null)
        {
            return forwardingEventsToProcess;
        }

        foreach (var syncedEvent in syncedEvents)
        {
            bool isDuplicate = (correspondence.ForwardingEvents ?? Enumerable.Empty<CorrespondenceForwardingEventEntity>())
                .Any(fe =>
                    fe.ForwardedOnDate.EqualsToSecond(syncedEvent.ForwardedOnDate)
                    && fe.ForwardedByPartyUuid == syncedEvent.ForwardedByPartyUuid
                    && fe.ForwardedByUserUuid == syncedEvent.ForwardedByUserUuid
                    && fe.ForwardedToUserId == syncedEvent.ForwardedToUserId
                    && fe.ForwardedToUserUuid == syncedEvent.ForwardedToUserUuid
                    && fe.ForwardedToEmailAddress == syncedEvent.ForwardedToEmailAddress
                    && fe.ForwardingText == syncedEvent.ForwardingText
                    && fe.MailboxSupplier == syncedEvent.MailboxSupplier);

            if (isDuplicate)
            {
                logger.LogInformation("Current Forwarding Event for {CorrespondenceId} has been deemed duplicate of existing and will be skipped. ForwardedOnDate: {ForwardedOnDate} - ForwardedByPartyUuid: {ForwardedByPartyUuid}",
                    correspondenceId, syncedEvent.ForwardedOnDate, syncedEvent.ForwardedByPartyUuid);
            }
            else
            {
                forwardingEventsToProcess.Add(syncedEvent);
            }
        }

        return forwardingEventsToProcess;
    }

    public async Task ProcessNotificationEvents(Guid correspondenceId, List<CorrespondenceNotificationEntity> notificationEvents, string operationName, CancellationToken cancellationToken)
    {
        if (notificationEvents.Count == 0)
        {
            return;
        }

        foreach (var notification in notificationEvents)
        {
            logger.LogInformation("Processing {OperationName} notification event for correspondence {CorrespondenceId} at {NotificationSent}",
                operationName, correspondenceId, notification.NotificationSent);

            notification.CorrespondenceId = correspondenceId;
            notification.Correspondence = null; // Clear navigation property to prevent EF Core from tracking the correspondence entity
            notification.SyncedFromAltinn2 = DateTimeOffset.UtcNow;
            var addedNotificationId = await correspondenceNotificationRepository.AddNotification(notification, cancellationToken);
            
            logger.LogDebug("Added new notification {NotificationId} for correspondence {CorrespondenceId}", addedNotificationId, correspondenceId);
        }

        logger.LogInformation("Successfully {OperationName}d {TotalEvents} notification events for correspondence {CorrespondenceId}",
            operationName, notificationEvents.Count, correspondenceId);
    }

    public async Task ProcessForwardingEvents(Guid correspondenceId, List<CorrespondenceForwardingEventEntity> forwardingEvents, string operationName, CancellationToken cancellationToken)
    {
        if (forwardingEvents.Count == 0)
        {
            return;
        }

        foreach (var forwardingEvent in forwardingEvents)
        {
            logger.LogInformation("Processing {OperationName} forwarding event for correspondence {CorrespondenceId} at {ForwardedOnDate}",
                operationName, correspondenceId, forwardingEvent.ForwardedOnDate);

            forwardingEvent.CorrespondenceId = correspondenceId;
            forwardingEvent.Correspondence = null; // Clear navigation property to prevent EF Core from tracking the correspondence entity
            forwardingEvent.SyncedFromAltinn2 = DateTimeOffset.UtcNow;
        }

        // Add the new forwarding events to the repository
        await correspondenceForwardingEventRepository.AddForwardingEvents(forwardingEvents, cancellationToken);

        logger.LogInformation("Successfully {OperationName}d {TotalEvents} forwarding events for correspondence {CorrespondenceId}",
            operationName, forwardingEvents.Count, correspondenceId);
    }

    /// <summary>
    /// Processes status and delete events for a correspondence in chronological order.
    /// This method contains the shared logic used by both sync and migration scenarios.
    /// </summary>
    /// <param name="correspondenceId">The correspondence ID</param>
    /// <param name="correspondence">The correspondence entity</param>
    /// <param name="statusEvents">List of status events to process (already filtered)</param>
    /// <param name="deleteEvents">List of delete events to process (already filtered)</param>
    /// <param name="operationName">Name of the operation for logging (e.g., "sync", "remigrate")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of events processed</returns>
    public async Task<int> ProcessEventsInChronologicalOrder(
        Guid correspondenceId,
        CorrespondenceEntity correspondence,
        List<CorrespondenceStatusEntity> statusEvents,
        List<CorrespondenceDeleteEventEntity> deleteEvents,
        string operationName,
        CancellationToken cancellationToken)
    {
        // Get dialog porten end user IDs for the events that need them
        var enduserIdsByPartyUuids = await GetDialogPortenEndUserIdsForEvents(statusEvents, deleteEvents, cancellationToken);

        // After filtering both collections, combine them into a single sorted collection, sorted by timestamp they occurred
        var allEventsToProcess = statusEvents
            .Select(e => new { EventType = "Status", Event = (object)e, Timestamp = e.StatusChanged })
            .Concat(deleteEvents
                .Select(e => new { EventType = "Delete", Event = (object)e, Timestamp = e.EventOccurred }))
            .OrderBy(e => e.Timestamp)
            .ToList();

        // Process events sequentially by chronological order to maintain granular control
        foreach (var evt in allEventsToProcess)
        {
            logger.LogInformation("Processing {OperationName} {EventType} event for correspondence {CorrespondenceId} at {Timestamp}",
                operationName, evt.EventType, correspondenceId, evt.Timestamp);

            try
            {
                if (evt.EventType == "Status")
                {
                    await ProcessStatusEvent(correspondenceId, correspondence, enduserIdsByPartyUuids, (CorrespondenceStatusEntity)evt.Event, cancellationToken);
                }
                else if (evt.EventType == "Delete")
                {
                    await ProcessDeleteEvent(correspondenceId, correspondence, enduserIdsByPartyUuids, (CorrespondenceDeleteEventEntity)evt.Event, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to {OperationName} {EventType} event for correspondence {CorrespondenceId} at {Timestamp}",
                    operationName, evt.EventType, correspondenceId, evt.Timestamp);
                throw; // Re-throw to trigger transaction rollback
            }
        }

        logger.LogInformation("Successfully {OperationName}d {TotalEvents} events for correspondence {CorrespondenceId}",
            operationName, allEventsToProcess.Count, correspondenceId);

        return allEventsToProcess.Count;
    }

    /// <summary>
    /// Processes all event types (status, delete, notification, forwarding) for a correspondence.
    /// This method handles filtering and processing all event types in sequence.
    /// </summary>
    /// <param name="correspondenceId">The correspondence ID</param>
    /// <param name="correspondence">The correspondence entity</param>
    /// <param name="statusEvents">List of status events to process (will be filtered)</param>
    /// <param name="deleteEvents">List of delete events to process (will be filtered)</param>
    /// <param name="notificationEvents">List of notification events to process (will be filtered)</param>
    /// <param name="forwardingEvents">List of forwarding events to process (will be filtered)</param>
    /// <param name="operationName">Name of the operation for logging (e.g., "sync", "remigrate")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Total number of events processed across all types</returns>
    public async Task<int> ProcessAllEventsForCorrespondence(
        Guid correspondenceId,
        CorrespondenceEntity correspondence,
        List<CorrespondenceStatusEntity>? statusEvents,
        List<CorrespondenceDeleteEventEntity>? deleteEvents,
        List<CorrespondenceNotificationEntity>? notificationEvents,
        List<CorrespondenceForwardingEventEntity>? forwardingEvents,
        string operationName,
        CancellationToken cancellationToken)
    {
        int totalEventsProcessed = 0;

        // Process status and delete events together (chronologically ordered as they interact with each other)
        if ((statusEvents != null && statusEvents.Count > 0) || (deleteEvents != null && deleteEvents.Count > 0))
        {
            var filteredStatusEvents = statusEvents != null ? FilterStatusEvents(correspondenceId, statusEvents, correspondence) : new List<CorrespondenceStatusEntity>();
            var filteredDeleteEvents = deleteEvents != null ? await FilterDeleteEvents(correspondenceId, deleteEvents, cancellationToken) : new List<CorrespondenceDeleteEventEntity>();
            
            if (filteredStatusEvents.Count > 0 || filteredDeleteEvents.Count > 0)
            {
                totalEventsProcessed += await ProcessEventsInChronologicalOrder(correspondenceId, correspondence, filteredStatusEvents, filteredDeleteEvents, operationName, cancellationToken);
            }
        }

        // Process notification events - Only provides information about notifications being sent, so they don't interact with status and delete events and can be processed independently after those
        if (notificationEvents != null && notificationEvents.Count > 0)
        {
            var filteredNotificationEvents = FilterNotificationEvents(correspondenceId, notificationEvents, correspondence);
            if (filteredNotificationEvents.Count > 0)
            {
                await ProcessNotificationEvents(correspondenceId, filteredNotificationEvents, operationName, cancellationToken);
                totalEventsProcessed += filteredNotificationEvents.Count;
            }
        }

        // Process forwarding events - Only provides information about forwarding events, so they don't interact with status and delete events and can be processed independently after those
        if (forwardingEvents != null && forwardingEvents.Count > 0)
        {
            var filteredForwardingEvents = FilterForwardingEvents(correspondenceId, forwardingEvents, correspondence);
            if (filteredForwardingEvents.Count > 0)
            {
                await ProcessForwardingEvents(correspondenceId, filteredForwardingEvents, operationName, cancellationToken);
                totalEventsProcessed += filteredForwardingEvents.Count;
            }
        }

        if (totalEventsProcessed > 0)
        {
            logger.LogInformation("Successfully {OperationName}d {TotalEvents} events total for correspondence {CorrespondenceId}",
                operationName, totalEventsProcessed, correspondenceId);
        }

        return totalEventsProcessed;
    }
}