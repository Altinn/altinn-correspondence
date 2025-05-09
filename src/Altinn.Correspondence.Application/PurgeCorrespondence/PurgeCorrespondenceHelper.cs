using Altinn.Correspondence.Application.CancelNotification;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Hangfire;

namespace Altinn.Correspondence.Application.PurgeCorrespondence;
public class PurgeCorrespondenceHelper
{
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly IStorageRepository _storageRepository;
    private readonly ICorrespondenceRepository _correspondenceRepository;
    private readonly ICorrespondenceStatusRepository _correspondenceStatusRepository;
    private readonly IAttachmentStatusRepository _attachmentStatusRepository;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IDialogportenService _dialogportenService;
    public PurgeCorrespondenceHelper(IAttachmentRepository attachmentRepository, IStorageRepository storageRepository, IAttachmentStatusRepository attachmentStatusRepository, ICorrespondenceRepository correspondenceRepository, ICorrespondenceStatusRepository correspondenceStatusRepository, IDialogportenService dialogportenService, IBackgroundJobClient backgroundJobClient)
    {
        _attachmentRepository = attachmentRepository;
        _storageRepository = storageRepository;
        _attachmentStatusRepository = attachmentStatusRepository;
        _backgroundJobClient = backgroundJobClient;
        _correspondenceRepository = correspondenceRepository;
        _correspondenceStatusRepository = correspondenceStatusRepository;
        _dialogportenService = dialogportenService;
    }
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
        var attachments = await _attachmentRepository.GetAttachmentsByCorrespondence(correspondenceId, cancellationToken);
        foreach (var attachment in attachments)
        {
            var canBeDeleted = await _attachmentRepository.CanAttachmentBeDeleted(attachment.Id, cancellationToken);
            if (!canBeDeleted || attachment.StatusHasBeen(AttachmentStatus.Purged))
            {
                continue;
            }

            await _storageRepository.PurgeAttachment(attachment.Id, attachment.StorageProvider, cancellationToken);
            var attachmentStatus = new AttachmentStatusEntity
            {
                AttachmentId = attachment.Id,
                Status = AttachmentStatus.Purged,
                StatusChanged = DateTimeOffset.UtcNow,
                StatusText = AttachmentStatus.Purged.ToString(),
                PartyUuid = partyUuid
            };
            await _attachmentStatusRepository.AddAttachmentStatus(attachmentStatus, cancellationToken);
        }
    }

    public async Task<Guid> PurgeCorrespondence(CorrespondenceEntity correspondence, bool isSender, Guid partyUuid, CancellationToken cancellationToken)
    {
        var status = isSender ? CorrespondenceStatus.PurgedByAltinn : CorrespondenceStatus.PurgedByRecipient;
        await _correspondenceStatusRepository.AddCorrespondenceStatus(new CorrespondenceStatusEntity()
        {
            CorrespondenceId = correspondence.Id,
            Status = status,
            StatusChanged = DateTimeOffset.UtcNow,
            StatusText = status.ToString(),
            PartyUuid = partyUuid
        }, cancellationToken);

        _backgroundJobClient.Enqueue<IEventBus>((eventBus) => eventBus.Publish(AltinnEventType.CorrespondencePurged, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, CancellationToken.None));
        await CheckAndPurgeAttachments(correspondence.Id, partyUuid, cancellationToken);
        await ReportActivityToDialogporten(isSender: isSender, correspondence.Id);
        CancelNotification(correspondence.Id, cancellationToken);
        var dialogId = correspondence.ExternalReferences.FirstOrDefault(externalReference => externalReference.ReferenceType == ReferenceType.DialogportenDialogId);
        if (dialogId is not null)
        {
            await _dialogportenService.SoftDeleteDialog(dialogId.ReferenceValue);
        }
        return correspondence.Id;
    }
    public async Task ReportActivityToDialogporten(bool isSender, Guid correspondenceId)
    {
        var actorType = isSender ? DialogportenActorType.Sender : DialogportenActorType.Recipient;
        var actorName = isSender ? "avsender" : "mottaker";
        await _dialogportenService.CreateCorrespondencePurgedActivity(correspondenceId, actorType, actorName);
    }

    public void CancelNotification(Guid correspondenceId, CancellationToken cancellationToken)
    {
        _backgroundJobClient.Enqueue<CancelNotificationHandler>(handler => handler.Process(null, correspondenceId, null, cancellationToken));
    }
}