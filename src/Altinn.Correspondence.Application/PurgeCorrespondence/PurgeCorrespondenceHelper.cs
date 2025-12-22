using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Hangfire;

namespace Altinn.Correspondence.Application.PurgeCorrespondence;
public class PurgeCorrespondenceHelper(
    IAttachmentRepository attachmentRepository,
    IAttachmentStatusRepository attachmentStatusRepository,
    ICorrespondenceStatusRepository correspondenceStatusRepository,
    IBackgroundJobClient backgroundJobClient,
    IDialogportenService dialogportenService,
    ICorrespondenceRepository correspondenceRepository)
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
            if (!canBeDeleted || attachment.StatusHasBeen(AttachmentStatus.Purged) || attachment.StatusHasBeen(AttachmentStatus.Expired))
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

    public async Task<Guid> PurgeCorrespondence(CorrespondenceEntity correspondence, bool isSender, Guid partyUuid, int partyId, DateTimeOffset operationTimestamp, CancellationToken cancellationToken, string? partyUrn)
    {
        var status = isSender ? CorrespondenceStatus.PurgedByAltinn : CorrespondenceStatus.PurgedByRecipient;
        await correspondenceStatusRepository.AddCorrespondenceStatus(new CorrespondenceStatusEntity()
        {
            CorrespondenceId = correspondence.Id,
            Status = status,
            StatusChanged = operationTimestamp,
            StatusText = status.ToString(),
            PartyUuid = partyUuid
        }, cancellationToken);

        backgroundJobClient.Enqueue<IEventBus>((eventBus) => eventBus.Publish(AltinnEventType.CorrespondencePurged, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, CancellationToken.None));
        if (correspondence.Altinn2CorrespondenceId.HasValue && correspondence.Altinn2CorrespondenceId > 0)
        {
            backgroundJobClient.Enqueue<IAltinnStorageService>(syncEventToAltinn2 => syncEventToAltinn2.SyncCorrespondenceEventToSblBridge(
                correspondence.Altinn2CorrespondenceId.Value,
                partyId,
                operationTimestamp,
                SyncEventType.Delete,
                CancellationToken.None));
        }
        
        await CheckAndPurgeAttachments(correspondence.Id, partyUuid, cancellationToken);
        var dialogId = correspondence.ExternalReferences.FirstOrDefault(externalReference => externalReference.ReferenceType == ReferenceType.DialogportenDialogId);
        if (dialogId is not null)
        {
            await ReportActivityToDialogporten(isSender: isSender, correspondence.Id, operationTimestamp, partyUrn);
            await ReportNotificationCancelledToDialogporten(correspondence.Id, operationTimestamp);
            await dialogportenService.SoftDeleteDialog(dialogId.ReferenceValue);
        }
        return correspondence.Id;
    }

    public async Task ReportActivityToDialogporten(bool isSender, Guid correspondenceId, DateTimeOffset operationTimestamp, string? partyUrn)
    {
        var actorType = isSender ? DialogportenActorType.Sender : DialogportenActorType.Recipient;
        var actorName = isSender ? "avsender" : "mottaker";
        await dialogportenService.CreateCorrespondencePurgedActivity(correspondenceId, actorType, actorName, operationTimestamp, partyUrn);
    }

    public async Task ReportNotificationCancelledToDialogporten(Guid correspondenceId, DateTimeOffset operationTimestamp)
    {
        var correspondence = await correspondenceRepository.GetCorrespondenceById(correspondenceId, false, false, false, CancellationToken.None);
        var notificationEntities = correspondence?.Notifications ?? [];
        foreach (var notification in notificationEntities)
        {
            if (notification.RequestedSendTime <= DateTimeOffset.UtcNow) continue; // Notification has already been sent
            await dialogportenService.CreateInformationActivity(notification.CorrespondenceId, DialogportenActorType.ServiceOwner, DialogportenTextType.NotificationOrderCancelled, operationTimestamp);
        }
    }
}
