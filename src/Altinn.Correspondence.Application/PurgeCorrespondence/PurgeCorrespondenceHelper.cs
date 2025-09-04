using Altinn.Correspondence.Application.CancelNotification;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.ProcessLegacyParty;
using Altinn.Correspondence.Application.SyncLegacyCorrespondenceEvent;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Hangfire;

namespace Altinn.Correspondence.Application.PurgeCorrespondence;
public class PurgeCorrespondenceHelper(
    IAttachmentRepository attachmentRepository,
    IStorageRepository storageRepository,
    IAttachmentStatusRepository attachmentStatusRepository,
    ICorrespondenceRepository correspondenceRepository,
    ICorrespondenceStatusRepository correspondenceStatusRepository,
    IDialogportenService dialogportenService,
    IBackgroundJobClient backgroundJobClient)
{
    public Error? ValidatePurgeRequestSender(CorrespondenceEntity correspondence)
    {
        var highestStatus = correspondence.GetHighestStatus();
        if (highestStatus == null)
        {
            return CorrespondenceErrors.CouldNotRetrieveStatus;
        }
        if (highestStatus.Status.IsPurged())
        {
            return CorrespondenceErrors.CorrespondenceAlreadyPurged;
        }
        if (!highestStatus.Status.IsPurgeableForSender())
        {
            return CorrespondenceErrors.CantPurgePublishedCorrespondence;
        }
        return null;
    }
    public Error? ValidatePurgeRequestRecipient(CorrespondenceEntity correspondence, bool IsLegacy = false)
    {
        var highestStatus = correspondence.GetHighestStatus();
        if (highestStatus == null)
        {
            return CorrespondenceErrors.CouldNotRetrieveStatus;
        }
        if (highestStatus.Status.IsPurged())
        {
            return CorrespondenceErrors.CorrespondenceAlreadyPurged;
        }
        if (!highestStatus.Status.IsAvailableForRecipient())
        {
            return CorrespondenceErrors.CorrespondenceNotFound;
        }
        if (!IsLegacy && correspondence.IsConfirmationNeeded && !correspondence.StatusHasBeen(CorrespondenceStatus.Confirmed))
        {
            return CorrespondenceErrors.ArchiveBeforeConfirmed;
        }
        return null;
    }
    public async Task CheckAndPurgeAttachments(Guid correspondenceId, Guid partyUuid, CancellationToken cancellationToken)
    {
        var attachments = await attachmentRepository.GetAttachmentsByCorrespondence(correspondenceId, cancellationToken);
        foreach (var attachment in attachments)
        {
            var canBeDeleted = await attachmentRepository.CanAttachmentBeDeleted(attachment.Id, cancellationToken);
            if (!canBeDeleted || attachment.StatusHasBeen(AttachmentStatus.Purged))
            {
                continue;
            }

            backgroundJobClient.Enqueue<IStorageRepository>(repository => repository.PurgeAttachment(attachment.Id, attachment.StorageProvider, CancellationToken.None));
            var attachmentStatus = new AttachmentStatusEntity
            {
                AttachmentId = attachment.Id,
                Status = AttachmentStatus.Purged,
                StatusChanged = DateTimeOffset.UtcNow,
                StatusText = AttachmentStatus.Purged.ToString(),
                PartyUuid = partyUuid
            };
            await attachmentStatusRepository.AddAttachmentStatus(attachmentStatus, cancellationToken);
        }
    }

    public async Task<Guid> PurgeCorrespondence(CorrespondenceEntity correspondence, bool isSender, Guid partyUuid, int partyId, DateTimeOffset operationTimestamp, CancellationToken cancellationToken)
    {
        var status = isSender ? CorrespondenceStatus.PurgedByAltinn : CorrespondenceStatus.PurgedByRecipient;
        await correspondenceStatusRepository.AddCorrespondenceStatus(new CorrespondenceStatusEntity()
        {
            CorrespondenceId = correspondence.Id,
            Status = status,
            StatusChanged = DateTimeOffset.UtcNow,
            StatusText = status.ToString(),
            PartyUuid = partyUuid
        }, cancellationToken);

        backgroundJobClient.Enqueue<IEventBus>((eventBus) => eventBus.Publish(AltinnEventType.CorrespondencePurged, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, CancellationToken.None));
        if (correspondence.Altinn2CorrespondenceId.HasValue)
        {
            backgroundJobClient.Enqueue<IAltinnStorageService>(
                syncEventToAltinn2 => syncEventToAltinn2.SyncCorrespondenceEventToSblBridge(partyId, correspondence.Altinn2CorrespondenceId.Value, operationTimestamp, SyncEventType.Delete, CancellationToken.None));
        }
        
        await CheckAndPurgeAttachments(correspondence.Id, partyUuid, cancellationToken);
        var reportToDialogportenJob = ReportActivityToDialogporten(isSender: isSender, correspondence.Id, operationTimestamp);
        var cancelNotificationJob = backgroundJobClient.ContinueJobWith<CancelNotificationHandler>(reportToDialogportenJob, 
            handler => handler.Process(null, correspondence.Id, null, cancellationToken));
        var dialogId = correspondence.ExternalReferences.FirstOrDefault(externalReference => externalReference.ReferenceType == ReferenceType.DialogportenDialogId);
        if (dialogId is not null)
        {
            backgroundJobClient.ContinueJobWith<IDialogportenService>(cancelNotificationJob, service => service.SoftDeleteDialog(dialogId.ReferenceValue));
        }
        return correspondence.Id;
    }
    public string ReportActivityToDialogporten(bool isSender, Guid correspondenceId, DateTimeOffset operationTimestamp)
    {
        var actorType = isSender ? DialogportenActorType.Sender : DialogportenActorType.Recipient;
        var actorName = isSender ? "avsender" : "mottaker";
        return backgroundJobClient.Enqueue<IDialogportenService>(service => service.CreateCorrespondencePurgedActivity(correspondenceId, actorType, actorName, operationTimestamp));
    }
}
