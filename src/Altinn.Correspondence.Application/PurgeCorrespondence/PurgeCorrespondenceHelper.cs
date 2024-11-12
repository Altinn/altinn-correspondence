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
    public Error? ValidateCurrentStatus(CorrespondenceEntity correspondence)
    {
        if (correspondence.Statuses.Any(status => status.Status.IsPurged()))
        {
            return Errors.CorrespondenceAlreadyPurged;
        }
        return null;
    }
    public Error? ValidatePurgeRequestSender(CorrespondenceEntity correspondence)
    {
        var latestStatus = correspondence.GetLatestStatus();
        if (latestStatus == null)
        {
            return Errors.CorrespondenceNotFound;
        }
        if (!latestStatus.Status.IsPurgeableForSender())
        {
            return Errors.CantPurgeCorrespondenceSender;
        }
        return null;
    }
    public Error? ValidatePurgeRequestRecipient(CorrespondenceEntity correspondence)
    {
        var latestStatus = correspondence.GetLatestStatus();
        if (latestStatus == null)
        {
            return Errors.CorrespondenceNotFound;
        }
        if (!latestStatus.Status.IsAvailableForRecipient())
        {
            return Errors.CorrespondenceNotFound;
        }
        if (correspondence.IsConfirmationNeeded && !correspondence.StatusHasBeen(CorrespondenceStatus.Confirmed))
        {
            return Errors.ArchiveBeforeConfirmed;
        }
        return null;
    }
    public async Task CheckAndPurgeAttachments(Guid correspondenceId, IAttachmentRepository attachmentRepository, IStorageRepository storageRepository, IAttachmentStatusRepository attachmentStatusRepository, CancellationToken cancellationToken)
    {
        var attachments = await attachmentRepository.GetAttachmentsByCorrespondence(correspondenceId, cancellationToken);
        foreach (var attachment in attachments)
        {
            var canBeDeleted = await attachmentRepository.CanAttachmentBeDeleted(attachment.Id, cancellationToken);
            if (!canBeDeleted || attachment.StatusHasBeen(AttachmentStatus.Purged))
            {
                continue;
            }

            await storageRepository.PurgeAttachment(attachment.Id, cancellationToken);
            var attachmentStatus = new AttachmentStatusEntity
            {
                AttachmentId = attachment.Id,
                Status = AttachmentStatus.Purged,
                StatusChanged = DateTimeOffset.UtcNow,
                StatusText = AttachmentStatus.Purged.ToString()
            };
            await attachmentStatusRepository.AddAttachmentStatus(attachmentStatus, cancellationToken);
        }
    }
    public void CreateInformationActivityDialogporten(bool isSender, Guid correspondenceId, IBackgroundJobClient backgroundJobClient)
    {
        if (isSender)
        {
            backgroundJobClient.Enqueue<IDialogportenService>((dialogportenService) => dialogportenService.CreateInformationActivity(correspondenceId, DialogportenActorType.Sender, DialogportenTextType.CorrespondencePurged, "avsender"));
        }
        else
        {
            backgroundJobClient.Enqueue<IDialogportenService>((dialogportenService) => dialogportenService.CreateInformationActivity(correspondenceId, DialogportenActorType.Recipient, DialogportenTextType.CorrespondencePurged, "mottaker"));
        }
    }
}