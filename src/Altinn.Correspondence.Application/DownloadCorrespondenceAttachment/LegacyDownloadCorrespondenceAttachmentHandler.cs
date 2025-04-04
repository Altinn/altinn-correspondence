using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
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
    IAltinnRegisterService altinnRegisterService) : IHandler<DownloadCorrespondenceAttachmentRequest, DownloadCorrespondenceAttachmentResponse>
{

    public async Task<OneOf<DownloadCorrespondenceAttachmentResponse, Error>> Process(DownloadCorrespondenceAttachmentRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
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
        var correspondence = await correspondenceRepository.GetCorrespondenceById(request.CorrespondenceId, true, false, false, cancellationToken);
        if (correspondence is null)
        {
            return CorrespondenceErrors.CorrespondenceNotFound;
        }
        var attachment = await attachmentRepository.GetAttachmentByCorrespondenceIdAndAttachmentId(request.CorrespondenceId, request.AttachmentId, cancellationToken);
        if (attachment is null)
        {
            return AttachmentErrors.AttachmentNotFound;
        }
        var latestStatus = correspondence.GetHighestStatus();
        if (!latestStatus.Status.IsAvailableForRecipient())
        {
            return CorrespondenceErrors.CorrespondenceNotFound;
        }
        var attachmentStream = await storageRepository.DownloadAttachment(attachment.Id, cancellationToken);
        backgroundJobClient.Enqueue<IDialogportenService>((dialogportenService) => dialogportenService.CreateInformationActivity(request.CorrespondenceId, DialogportenActorType.Recipient, DialogportenTextType.DownloadStarted, attachment.DisplayName ?? attachment.FileName));
        return new DownloadCorrespondenceAttachmentResponse()
        {
            FileName = attachment.FileName,
            Stream = attachmentStream
        };
    }
}
