using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Altinn.Correspondence.Core.Models.Enums;
using Hangfire;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.DownloadCorrespondenceAttachment;

public class LegacyDownloadCorrespondenceAttachmentHandler(
    IStorageRepository storageRepository,
    IAttachmentRepository attachmentRepository,
    ICorrespondenceRepository correspondenceRepository,
    UserClaimsHelper userClaimsHelper,
    IBackgroundJobClient backgroundJobClient,
    AttachmentHelper attachmentHelper,
    IAltinnRegisterService altinnRegisterService) : IHandler<DownloadCorrespondenceAttachmentRequest, DownloadCorrespondenceAttachmentResponse>
{

    public async Task<OneOf<DownloadCorrespondenceAttachmentResponse, Error>> Process(DownloadCorrespondenceAttachmentRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var operationTimestamp = DateTimeOffset.UtcNow;
        var partyId = userClaimsHelper.GetPartyId();
        if (partyId is null)
        {
            return AuthorizationErrors.InvalidPartyId;
        }
        var party = await altinnRegisterService.LookUpPartyByPartyId(partyId.Value, cancellationToken);
        if (party is null || (string.IsNullOrEmpty(party.SSN) && string.IsNullOrEmpty(party.OrgNumber)))
        {
            return AuthorizationErrors.CouldNotFindOrgNo;
        }
        // TODO: Authorize party
        var correspondence = await correspondenceRepository.GetCorrespondenceById(request.CorrespondenceId, true, false, false, cancellationToken, true);
        if (correspondence is null)
        {
            return CorrespondenceErrors.CorrespondenceNotFound;
        }
        var attachment = await attachmentRepository.GetAttachmentByCorrespondenceIdAndAttachmentId(request.CorrespondenceId, request.AttachmentId, cancellationToken);
        if (attachment is null)
        {
            return AttachmentErrors.AttachmentNotFound;
        }
        var correspondenceAttachment = correspondence.Content?.Attachments?.FirstOrDefault(a => a.AttachmentId == request.AttachmentId);
        var cannotDownloadAttachmentError = attachmentHelper.ValidateDownloadCorrespondenceAttachment(attachment, correspondenceAttachment?.ExpirationTime);
        if (cannotDownloadAttachmentError is not null)
        {
            return cannotDownloadAttachmentError;
        }
        var latestStatus = correspondence.GetHighestStatusForLegacyCorrespondence();
        if (!latestStatus.Status.IsAvailableForLegacyRecipient())
        {
            return CorrespondenceErrors.CorrespondenceNotFound;
        }
        string caller = party.SSN;
        if (string.IsNullOrEmpty(caller))
        {
            caller = party.OrgNumber;
        }
        var attachmentStream = await storageRepository.DownloadAttachment(attachment.Id, attachment.StorageProvider, cancellationToken);
        backgroundJobClient.Enqueue<IDialogportenService>((dialogportenService) => dialogportenService.CreateDownloadStartedActivity(request.CorrespondenceId, DialogportenActorType.Recipient, operationTimestamp, caller, attachment.DisplayName ?? attachment.FileName, attachment.Id.ToString()));
        return new DownloadCorrespondenceAttachmentResponse()
        {
            FileName = attachment.FileName,
            Stream = attachmentStream
        };
    }
}
