using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.PurgeCorrespondence;
using Altinn.Correspondence.Common.Constants;
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
    ICorrespondenceStatusRepository correspondenceStatusRepository,
    ICorrespondenceDeleteEventRepository correspondenceDeleteEventRepository,
    IAltinnRegisterService altinnRegisterService,
    PurgeCorrespondenceHelper purgeCorrespondenceHelper,
    IBackgroundJobClient backgroundJobClient,
    ILogger<SyncCorrespondenceStatusEventHandler> logger) : IHandler<SyncCorrespondenceStatusEventRequest, Guid>
{
    /// <summary>
    /// Processes a sync request containing status and delete events for a single correspondence coming from Altinn 2.
    /// </summary>
    /// <remarks>
    /// Validates and de-duplicates incoming events, persists new status and delete events, and triggers related side effects:
    /// - Enqueues Dialogporten background jobs and event-bus publications for status events (Confirmed, Read, Archived) when the correspondence is fully migrated.
    /// - Performs purge, soft-delete or restore flows for delete events, including attachment purge and Dialogporten updates when applicable.
    /// Duplicate or invalid events are ignored. Dialogporten updates are skipped for correspondences that are still migrating.
    /// </remarks>
    /// <param name="request">Request containing the correspondence Id and lists of synced status and delete events to process.</param>
    /// <param name="user">Optional caller principal (not required for processing; included for context/audit).</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>
    /// On success returns the processed correspondence Id (Guid). On failure returns an Error value (for example when the correspondence is not found).
    /// </returns>
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

        // Only fetch Dialogporten enduserIds if we have events to execute and the correspondence is fully migrated (IsMigrating = false)
        Dictionary<Guid, string> enduserIdByPartyUuid = new Dictionary<Guid, string>();
        if (correspondence.IsMigrating == false && (statusEventsToExecute.Count > 0 || deletionEventsToExecute.Count > 0 ))
        {
            enduserIdByPartyUuid = await GetDialogPortenEndUserIdsForEvents(statusEventsToExecute, deletionEventsToExecute, cancellationToken);
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
                                    await SetArchivedOnCorrespondenceDialog(correspondence, eventToExecute, enduserIdByPartyUuid, cancellationToken);
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
                            await SoftDeleteOrRestoreCorrespondence(correspondence, deletionEvent, enduserIdByPartyUuid, cancellationToken);
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

    /// <summary>
    /// Builds a mapping from party UUID to Dialogporten enduserId for events that require a Dialogporten user identifier.
    /// </summary>
    /// <param name="statusEventsToExecute">Status events to consider; only events with <see cref="CorrespondenceStatus.Archived"/> require an enduserId.</param>
    /// <param name="deletionEventsToExecute">Deletion events to consider; only <see cref="CorrespondenceDeleteEventType.SoftDeletedByRecipient"/> and <see cref="CorrespondenceDeleteEventType.RestoredByRecipient"/> require an enduserId.</param>
    /// <param name="cancellationToken">Cancellation token for async lookups.</param>
    /// <returns>
    /// A dictionary mapping party UUIDs to Dialogporten enduserId strings in the form "urn:person:&lt;SSN&gt;" for parties that could be resolved and are of type Person.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when a required party UUID cannot be found in Altinn Register.</exception>
    /// <remarks>
    /// Parties that are not of type Person are skipped (a warning is logged) and are not included in the returned dictionary.
    /// Duplicate UUIDs across inputs are handled and looked up only once.
    /// </remarks>
    private async Task<Dictionary<Guid, string>> GetDialogPortenEndUserIdsForEvents(List<CorrespondenceStatusEntity> statusEventsToExecute, List<CorrespondenceDeleteEventEntity> deletionEventsToExecute, CancellationToken cancellationToken)
    {
        var enduserIdByPartyUuid = new Dictionary<Guid, string>();
        var partyUuidsToLookup = statusEventsToExecute
            .Where(e => e.Status == CorrespondenceStatus.Archived) // Only Archived status events require Dialogporten enduserId
            .Select(e => e.PartyUuid)
            .Distinct();

        partyUuidsToLookup = partyUuidsToLookup
           .Concat(deletionEventsToExecute
               .Where(e => e.EventType == CorrespondenceDeleteEventType.SoftDeletedByRecipient || e.EventType == CorrespondenceDeleteEventType.RestoredByRecipient) // Only SoftDelete and Restore events require Dialogporten enduserId
               .Select(e => e.PartyUuid)
               .Distinct()
           )
           .Distinct()
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

    /// <summary>
    /// Apply a hard purge to a correspondence based on a hard-delete event: persist a corresponding Purged status, record the delete event, purge attachments, and (when not migrating) publish a purge event and create a Dialogporten purged activity followed by a soft-delete of the dialog.
    /// </summary>
    /// <param name="correspondence">The correspondence to purge (must contain ExternalReferences for Dialogporten when not migrating).</param>
    /// <param name="deleteEventToSync">The delete event that triggered the purge; must have EventType of HardDeletedByServiceOwner or HardDeletedByRecipient and a valid EventOccurred timestamp and PartyUuid.</param>
    /// <param name="cancellationToken">Cancellation token for async operations.</param>
    /// <returns>The Id of the purged correspondence.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="deleteEventToSync"/> has an unsupported EventType, or when a Dialogporten dialog id is required but missing on the correspondence.</exception>
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

    /// <summary>
    /// Persists a recipient soft-delete or restore event for a correspondence and, when applicable,
    /// updates the associated Dialogporten dialog state.
    /// </summary>
    /// <remarks>
    /// - Accepts only <see cref="CorrespondenceDeleteEventType.SoftDeletedByRecipient"/> or
    ///   <see cref="CorrespondenceDeleteEventType.RestoredByRecipient"/>; other event types cause an <see cref="ArgumentException"/>.
    /// - The delete event is stored unconditionally. Dialogporten is updated only when the correspondence is not migrating,
    ///   the correspondence has not been purged, and an enduserId is available for the event's PartyUuid.
    /// - If the correspondence has been purged or the enduserId is missing, the method logs a warning and skips dialog updates.
    /// </remarks>
    /// <param name="deleteEventToSync">The delete event to persist and process (must be SoftDeletedByRecipient or RestoredByRecipient).</param>
    /// <param name="enduserIdByPartyUuid">Mapping from PartyUuid to Dialogporten enduserId; used to look up the enduserId for the event's party.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="deleteEventToSync"/> has an unsupported <see cref="CorrespondenceDeleteEventType"/>.</exception>
    private async Task SoftDeleteOrRestoreCorrespondence(CorrespondenceEntity correspondence, CorrespondenceDeleteEventEntity deleteEventToSync, Dictionary<Guid, string> enduserIdByPartyUuid, CancellationToken cancellationToken)
    {
        DateTimeOffset syncedTimestamp = DateTimeOffset.UtcNow;
        if (CorrespondenceDeleteEventType.SoftDeletedByRecipient != deleteEventToSync.EventType && CorrespondenceDeleteEventType.RestoredByRecipient != deleteEventToSync.EventType)
        {
            throw new ArgumentException($"Cannot perform SoftDeleteOrRestoreCorrespondence for {deleteEventToSync.EventType}");
        }
        
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
                bool isArchived = correspondence.StatusHasBeen(CorrespondenceStatus.Archived);

                // Perform SoftDelete or Restore in Dialogporten
                await SetSoftDeleteOrRestoreOnDialog(correspondence.Id, enduserIdByPartyUuid[deleteEventToSync.PartyUuid], deleteEventToSync.EventType, correspondence.StatusHasBeen(CorrespondenceStatus.Archived), cancellationToken);
            }
        }
    }

    /// <summary>
    /// If a Dialogporten end-user id exists for the status event's party, enqueue a background job to apply the Archive system label to the correspondence's dialog; otherwise log and skip the update.
    /// </summary>
    /// <param name="statusEventToSync">Status event whose PartyUuid is used to look up the Dialogporten end-user id.</param>
    /// <param name="enduserIdByPartyUuid">Mapping of PartyUuid to Dialogporten end-user id; the method looks up the id for <paramref name="statusEventToSync"/>.PartyUuid and skips the update if missing.</param>
    private async Task SetArchivedOnCorrespondenceDialog(CorrespondenceEntity correspondence, CorrespondenceStatusEntity statusEventToSync, Dictionary<Guid, string> enduserIdByPartyUuid, CancellationToken cancellationToken)
    {
        if (!enduserIdByPartyUuid.ContainsKey(statusEventToSync.PartyUuid))
        {
            logger.LogWarning("Skipping updating dialog with SystemLabel Archived for correspondence {CorrespondenceId} at {StatusChanged} due to missing Dialogporten enduserId for party {PartyUuid}.", correspondence.Id, statusEventToSync.StatusChanged, statusEventToSync.PartyUuid);
        }
        else
        {
            backgroundJobClient.Enqueue<IDialogportenService>(service => service.UpdateSystemLabelsOnDialog(correspondence.Id, enduserIdByPartyUuid[statusEventToSync.PartyUuid], new List<DialogPortenSystemLabel> { DialogPortenSystemLabel.Archive }, null));
        }   
    }

    /// <summary>
    /// Associates a delete event with the given correspondence, sets the Altinn‑2 synced timestamp, and persists the event.
    /// </summary>
    /// <param name="correspondence">The correspondence to associate the delete event with.</param>
    /// <param name="deleteEventToSync">The delete event to store; its CorrespondenceId and SyncedFromAltinn2 will be set.</param>
    /// <param name="syncedTimestamp">Timestamp to record in <c>SyncedFromAltinn2</c> indicating when the event was synced.</param>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    private async Task StoreDeleteEventForCorrespondence(CorrespondenceEntity correspondence, CorrespondenceDeleteEventEntity deleteEventToSync, DateTimeOffset syncedTimestamp, CancellationToken cancellationToken)
    {
        deleteEventToSync.CorrespondenceId = correspondence.Id;
        deleteEventToSync.SyncedFromAltinn2 = syncedTimestamp;
        await correspondenceDeleteEventRepository.AddDeleteEvent(deleteEventToSync, cancellationToken);
    }

    /// <summary>
    /// Enqueues a Dialogporten update to apply or remove system labels on the dialog for a recipient soft-delete or restore event.
    /// </summary>
    /// <remarks>
    /// - For <see cref="CorrespondenceDeleteEventType.SoftDeletedByRecipient"/>, enqueues a job to add the "Bin" label.
    /// - For <see cref="CorrespondenceDeleteEventType.RestoredByRecipient"/>, enqueues a job to either add the "Archive" label (if <paramref name="isArchived"/> is true)
    ///   or remove the "Bin" label (if <paramref name="isArchived"/> is false).
    /// </remarks>
    /// <param name="correspondenceId">The identifier of the correspondence whose dialog should be updated.</param>
    /// <param name="endUserId">The Dialogporten end-user identifier for the recipient (prefixed URN).</param>
    /// <param name="eventType">The delete event type determining whether to soft-delete or restore labels.</param>
    /// <param name="isArchived">True if the correspondence is currently archived; affects label choice when restoring.</param>
    /// <param name="cancellationToken">Cancellation token for the asynchronous operation.</param>
    /// <exception cref="ArgumentException">Thrown when <paramref name="eventType"/> is not supported by this operation.</exception>
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
