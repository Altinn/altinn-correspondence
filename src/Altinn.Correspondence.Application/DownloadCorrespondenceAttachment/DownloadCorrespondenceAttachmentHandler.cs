using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Hangfire;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.DownloadCorrespondenceAttachment;

public class DownloadCorrespondenceAttachmentHandler : IHandler<DownloadCorrespondenceAttachmentRequest, DownloadCorrespondenceAttachmentResponse>
{
    private readonly ICorrespondenceRepository _correspondenceRepository;
    private readonly IAltinnAuthorizationService _altinnAuthorizationService;
    private readonly IStorageRepository _storageRepository;
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly UserClaimsHelper _userClaimsHelper;
    private readonly IBackgroundJobClient _backgroundJobClient;

    public DownloadCorrespondenceAttachmentHandler(IAltinnAuthorizationService altinnAuthorizationService, IStorageRepository storageRepository, IAttachmentRepository attachmentRepository, ICorrespondenceRepository correspondenceRepository, UserClaimsHelper userClaimsHelper, IBackgroundJobClient backgroundJobClient)
    {
        _correspondenceRepository = correspondenceRepository;
        _altinnAuthorizationService = altinnAuthorizationService;
        _storageRepository = storageRepository;
        _attachmentRepository = attachmentRepository;
        _userClaimsHelper = userClaimsHelper;
        _backgroundJobClient = backgroundJobClient;
    }

    public async Task<OneOf<DownloadCorrespondenceAttachmentResponse, Error>> Process(DownloadCorrespondenceAttachmentRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(request.CorrespondenceId, true, false, cancellationToken);
        if (correspondence is null)
        {
            return Errors.CorrespondenceNotFound;
        }
        var attachment = await _attachmentRepository.GetAttachmentByCorrespondenceIdAndAttachmentId(request.CorrespondenceId, request.AttachmentId, cancellationToken);
        if (attachment is null)
        {
            return Errors.AttachmentNotFound;
        }
        string? onBehalfOf = request.OnBehalfOf;
        bool isOnBehalfOfRecipient = false;
        if (!string.IsNullOrEmpty(onBehalfOf))
        {
            isOnBehalfOfRecipient = correspondence.Recipient.GetOrgNumberWithoutPrefix() == onBehalfOf.GetOrgNumberWithoutPrefix();
        }
        var hasAccess = await _altinnAuthorizationService.CheckUserAccess(
            user,
            correspondence.ResourceId,
            request.OnBehalfOf ?? correspondence.Recipient,
            correspondence.Id.ToString(),
            [ResourceAccessLevel.Read],
            cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }
        var isRecipient = _userClaimsHelper.IsRecipient(correspondence.Recipient) || isOnBehalfOfRecipient;
        if (!isRecipient)
        {
            return Errors.CorrespondenceNotFound;
        }
        var latestStatus = correspondence.GetLatestStatus();
        if (!latestStatus.Status.IsAvailableForRecipient())
        {
            return Errors.CorrespondenceNotFound;
        }
        var attachmentStream = await _storageRepository.DownloadAttachment(attachment.Id, cancellationToken);
        _backgroundJobClient.Enqueue<IDialogportenService>((dialogportenService) => dialogportenService.CreateInformationActivity(request.CorrespondenceId, Core.Services.Enums.DialogportenActorType.Recipient, DialogportenTextType.DownloadStarted, attachment.FileName ?? attachment.Name));
        return new DownloadCorrespondenceAttachmentResponse(){
            FileName = attachment.FileName,
            Stream = attachmentStream
        };
    }
}
