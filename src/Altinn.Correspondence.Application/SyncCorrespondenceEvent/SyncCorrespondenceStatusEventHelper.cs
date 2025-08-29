using Altinn.Correspondence.Application.PurgeCorrespondence;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Hangfire;

namespace Altinn.Correspondence.Application.SyncCorrespondenceEvent;
public class SyncCorrespondenceStatusEventHelper(    
    ICorrespondenceStatusRepository correspondenceStatusRepository,
    ICorrespondenceDeleteEventRepository correspondenceDeleteEventRepository,
    IDialogportenService dialogportenService,
    IAltinnRegisterService altinnRegisterService,
    IBackgroundJobClient backgroundJobClient,
    PurgeCorrespondenceHelper purgeCorrespondenceHelper)
{

    /// <summary>
    /// Validates if the current status of the correspondence should be synced
    /// </summary>
    /// <param name="correspondence">The correspondence entity to validate</param>
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

    public async Task AddSyncedCorrespondenceStatuses(CorrespondenceEntity correspondence, List<CorrespondenceStatusEntity> statuses, CancellationToken cancellationToken)
    {
        foreach (var entity in statuses)
        {
            entity.CorrespondenceId = correspondence.Id;
            entity.SyncedFromAltinn2 = DateTimeOffset.UtcNow;
            entity.StatusText = $"Synced event {entity.Status} from Altinn 2";
        }

        await correspondenceStatusRepository.AddCorrespondenceStatuses(statuses, cancellationToken);
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

        deleteEventToSync.CorrespondenceId = correspondence.Id;
        deleteEventToSync.SyncedFromAltinn2 = syncedTimestamp;        
        await correspondenceDeleteEventRepository.AddDeleteEvent(deleteEventToSync, cancellationToken);

        if (correspondence.IsMigrating == false)
        {
            backgroundJobClient.Enqueue<IEventBus>((eventBus) => eventBus.Publish(AltinnEventType.CorrespondencePurged, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, CancellationToken.None)); 
        }
        
        await purgeCorrespondenceHelper.CheckAndPurgeAttachments(correspondence.Id, deleteEventToSync.PartyUuid, cancellationToken);

        if (correspondence.IsMigrating == false)
        {
            var reportToDialogportenJob = ReportPurgedActivityToDialogporten(deleteEventToSync.EventType, correspondence.Id, deleteEventToSync.EventOccurred);

            // TODO: fix soft delete dialog
            // backgroundJobClient.ContinueJobWith<IDialogportenService>(reportToDialogportenJob, service => service.SoftDeleteDialog(dialogId));
        }

        return correspondence.Id;
    }

    public async Task SoftDeleteOrRestoreCorrespondence(CorrespondenceEntity correspondence, CorrespondenceDeleteEventEntity deleteEventToSync, CancellationToken cancellationToken)
    {   
        DateTimeOffset syncedTimestamp = DateTimeOffset.UtcNow;
        if(CorrespondenceDeleteEventType.SoftDeletedByRecipient != deleteEventToSync.EventType && CorrespondenceDeleteEventType.RestoredByRecipient != deleteEventToSync.EventType)
        {
            throw new ArgumentException($"Cannot perform SoftDeleteOrRestoreCorrespondence for {deleteEventToSync.EventType}");
        }

        deleteEventToSync.CorrespondenceId = correspondence.Id;
        deleteEventToSync.SyncedFromAltinn2 = syncedTimestamp;
        await correspondenceDeleteEventRepository.AddDeleteEvent(deleteEventToSync, cancellationToken);

        if (correspondence.IsMigrating == false)
        {
            await SetSoftDeleteOrRestoreOnDialog(correspondence.Id, deleteEventToSync.PartyUuid, deleteEventToSync.EventType, cancellationToken);
        }
    }

    public async Task SetSoftDeleteOrRestoreOnDialog(Guid correspondenceId, Guid partyUuid, CorrespondenceDeleteEventType eventType, CancellationToken cancellationToken)
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

    public string ReportPurgedActivityToDialogporten(CorrespondenceDeleteEventType eventType, Guid correspondenceId, DateTimeOffset operationTimestamp)
    {
        var actorType = eventType == CorrespondenceDeleteEventType.HardDeletedByServiceOwner ? DialogportenActorType.Sender : DialogportenActorType.Recipient;
        var actorName = eventType == CorrespondenceDeleteEventType.HardDeletedByServiceOwner ? "avsender" : "mottaker";
        return backgroundJobClient.Enqueue<IDialogportenService>(service => service.CreateCorrespondencePurgedActivity(correspondenceId, actorType, actorName, operationTimestamp));
    }

    public string SoftDeleteDialogInDialogporten(string dialogId)
    {
        return backgroundJobClient.Enqueue<IDialogportenService>(service => service.SoftDeleteDialog(dialogId));
    }

    public async Task ReportArchivedToDialogporten(Guid correspondenceId, Guid enduserPartyUuid, CancellationToken cancellationToken)
    {
        var endUserParty = await altinnRegisterService.LookUpPartyByPartyUuid(enduserPartyUuid, cancellationToken);
        if (endUserParty is null)
        {
            throw new ArgumentException($"Party with UUID {enduserPartyUuid} not found in Altinn Register - cannot set archived Systemlabel for correspondence {correspondenceId}.");
        }

        backgroundJobClient.Enqueue<IDialogportenService>(service => service.UpdateSystemLabelsOnDialog(correspondenceId, GetPrefixedIdentifierForParty(endUserParty), new List<string> { "Archive" }, null));
    }

    public void ReportReadToDialogporten(Guid correspondenceId, DateTimeOffset operationTimestamp)
    {
        backgroundJobClient.Enqueue<IDialogportenService>((dialogportenService) => dialogportenService.CreateOpenedActivity(correspondenceId, DialogportenActorType.Recipient, operationTimestamp));
    }

    private string GetPrefixedIdentifierForParty(Party party)
    {
        if(party.PartyTypeName == PartyType.Organization)
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