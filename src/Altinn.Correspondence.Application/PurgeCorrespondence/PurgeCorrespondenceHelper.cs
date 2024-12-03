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
    private readonly IAttachmentStatusRepository _attachmentStatusRepository;
    private readonly IBackgroundJobClient _backgroundJobClient;
    public PurgeCorrespondenceHelper(IAttachmentRepository attachmentRepository, IStorageRepository storageRepository, IAttachmentStatusRepository attachmentStatusRepository, IBackgroundJobClient backgroundJobClient)
    {
        _attachmentRepository = attachmentRepository;
        _storageRepository = storageRepository;
        _attachmentStatusRepository = attachmentStatusRepository;
        _backgroundJobClient = backgroundJobClient;
    }
    public Error? ValidateCurrentStatus(CorrespondenceEntity correspondence)
    {
        if (correspondence.Statuses is not null && correspondence.Statuses.Any(status => status.Status.IsPurged()))
        {
            return CorrespondenceErrors.CorrespondenceAlreadyPurged;
        }
        return null;
    }
    public Error? ValidatePurgeRequestSender(CorrespondenceEntity correspondence)
    {
        var latestStatus = correspondence.GetHighestStatus();
        if (latestStatus == null)
        {
            return CorrespondenceErrors.CorrespondenceNotFound;
        }
        if (!latestStatus.Status.IsPurgeableForSender())
        {
            return CorrespondenceErrors.CantPurgePublishedCorrespondence;
        }
        return null;
    }
    public Error? ValidatePurgeRequestRecipient(CorrespondenceEntity correspondence)
    {
        var latestStatus = correspondence.GetHighestStatus();
        if (latestStatus == null)
        {
            return CorrespondenceErrors.CorrespondenceNotFound;
        }
        if (!latestStatus.Status.IsAvailableForRecipient())
        {
            return CorrespondenceErrors.CorrespondenceNotFound;
        }
        if (correspondence.IsConfirmationNeeded && !correspondence.StatusHasBeen(CorrespondenceStatus.Confirmed))
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

            await _storageRepository.PurgeAttachment(attachment.Id, cancellationToken);
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
    public void ReportActivityToDialogporten(bool isSender, Guid correspondenceId)
    {
        if (isSender)
        {
            _backgroundJobClient.Enqueue<IDialogportenService>((dialogportenService) => dialogportenService.CreateInformationActivity(correspondenceId, DialogportenActorType.Sender, DialogportenTextType.CorrespondencePurged, "avsender"));
        }
        else
        {
            _backgroundJobClient.Enqueue<IDialogportenService>((dialogportenService) => dialogportenService.CreateInformationActivity(correspondenceId, DialogportenActorType.Recipient, DialogportenTextType.CorrespondencePurged, "mottaker"));
        }
    }
    public void CancelNotification(Guid correspondenceId, CancellationToken cancellationToken)
    {
        _backgroundJobClient.Enqueue<CancelNotificationHandler>(handler => handler.Process(null, correspondenceId, null, cancellationToken));
    }
}