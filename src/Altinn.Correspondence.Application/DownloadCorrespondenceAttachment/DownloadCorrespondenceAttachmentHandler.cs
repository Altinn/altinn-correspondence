using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Hangfire;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.DownloadCorrespondenceAttachment;

public class DownloadCorrespondenceAttachmentHandler(
    IAltinnAuthorizationService altinnAuthorizationService,
    IStorageRepository storageRepository,
    IAttachmentRepository attachmentRepository,
    ICorrespondenceRepository correspondenceRepository,
    IBackgroundJobClient backgroundJobClient) : IHandler<DownloadCorrespondenceAttachmentRequest, DownloadCorrespondenceAttachmentResponse>
{

    public async Task<OneOf<DownloadCorrespondenceAttachmentResponse, Error>> Process(DownloadCorrespondenceAttachmentRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var correspondence = await correspondenceRepository.GetCorrespondenceById(request.CorrespondenceId, true, false, cancellationToken);
        if (correspondence is null)
        {
            return CorrespondenceErrors.CorrespondenceNotFound;
        }
        var attachment = await attachmentRepository.GetAttachmentByCorrespondenceIdAndAttachmentId(request.CorrespondenceId, request.AttachmentId, cancellationToken);
        if (attachment is null)
        {
            return AttachmentErrors.AttachmentNotFound;
        }
        var hasAccess = await altinnAuthorizationService.CheckAccessAsRecipient(user, correspondence, cancellationToken);
        if (!hasAccess)
        {
            return AuthorizationErrors.NoAccessToResource;
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
