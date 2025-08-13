using Altinn.Correspondence.Application.PurgeCorrespondence;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Hangfire;

namespace Altinn.Correspondence.Application.SyncCorrespondenceEvent;
public class SyncCorrespondenceStatusEventHelper(    
    ICorrespondenceStatusRepository correspondenceStatusRepository,
    IDialogportenService dialogportenService,
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
        var validStatuses = new[] { CorrespondenceStatus.Read, CorrespondenceStatus.Confirmed, CorrespondenceStatus.Archived, CorrespondenceStatus.PurgedByAltinn, CorrespondenceStatus.PurgedByRecipient };

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
            entity.SyncedFromAltinn2 = DateTime.UtcNow;
        }

        await correspondenceStatusRepository.AddCorrespondenceStatuses(statuses, cancellationToken);
    }

    public async Task<Guid> PurgeCorrespondence(CorrespondenceEntity correspondence, CorrespondenceStatusEntity statusToSync, bool isAvailable, CancellationToken cancellationToken)
    {   
        await correspondenceStatusRepository.AddCorrespondenceStatus(new CorrespondenceStatusEntity()
        {
            CorrespondenceId = correspondence.Id,
            Status = statusToSync.Status,
            StatusChanged = statusToSync.StatusChanged,
            StatusText = statusToSync.Status.ToString(),
            PartyUuid = statusToSync.PartyUuid,            
            SyncedFromAltinn2 = DateTime.UtcNow
        }, cancellationToken);

        if (isAvailable)
        {
            backgroundJobClient.Enqueue<IEventBus>((eventBus) => eventBus.Publish(AltinnEventType.CorrespondencePurged, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, CancellationToken.None)); 
        }
        
        await purgeCorrespondenceHelper.CheckAndPurgeAttachments(correspondence.Id, statusToSync.PartyUuid, cancellationToken);

        if (isAvailable)
        {
            var reportToDialogportenJob = ReportActivityToDialogporten(statusToSync.Status, correspondence.Id, statusToSync.StatusChanged);
        }         
        
        return correspondence.Id;
    }

    public string ReportActivityToDialogporten(CorrespondenceStatus status, Guid correspondenceId, DateTimeOffset operationTimestamp)
    {
        var actorType = status == CorrespondenceStatus.PurgedByAltinn ? DialogportenActorType.Sender : DialogportenActorType.Recipient;
        var actorName = status == CorrespondenceStatus.PurgedByAltinn ? "avsender" : "mottaker";
        return backgroundJobClient.Enqueue<IDialogportenService>(service => service.CreateCorrespondencePurgedActivity(correspondenceId, actorType, actorName, operationTimestamp));
    }
}