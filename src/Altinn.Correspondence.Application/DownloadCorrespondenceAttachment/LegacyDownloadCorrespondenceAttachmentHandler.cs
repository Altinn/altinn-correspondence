using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Hangfire;
using OneOf;

namespace Altinn.Correspondence.Application.DownloadCorrespondenceAttachment;

public class LegacyDownloadCorrespondenceAttachmentHandler : IHandler<DownloadCorrespondenceAttachmentRequest, DownloadCorrespondenceAttachmentResponse>
{
    private readonly ICorrespondenceRepository _correspondenceRepository;
    private readonly IAltinnAuthorizationService _altinnAuthorizationService;
    private readonly IStorageRepository _storageRepository;
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly UserClaimsHelper _userClaimsHelper;
    private readonly IBackgroundJobClient _backgroundJobClient;

    public LegacyDownloadCorrespondenceAttachmentHandler(IAltinnAuthorizationService altinnAuthorizationService, IStorageRepository storageRepository, IAttachmentRepository attachmentRepository, ICorrespondenceRepository correspondenceRepository, UserClaimsHelper userClaimsHelper, IBackgroundJobClient backgroundJobClient)
    {
        _correspondenceRepository = correspondenceRepository;
        _altinnAuthorizationService = altinnAuthorizationService;
        _storageRepository = storageRepository;
        _attachmentRepository = attachmentRepository;
        _userClaimsHelper = userClaimsHelper;
        _backgroundJobClient = backgroundJobClient;
    }

    public async Task<OneOf<DownloadCorrespondenceAttachmentResponse, Error>> Process(DownloadCorrespondenceAttachmentRequest request, CancellationToken cancellationToken)
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
        bool isRecipient = true; // TODO: Authorize user has claims to download attachment
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
        _backgroundJobClient.Enqueue<IDialogportenService>((dialogportenService) => dialogportenService.CreateInformationActivity(request.CorrespondenceId, DialogportenActorType.Recipient, DialogportenTextType.DownloadStarted, attachment.FileName ?? attachment.Name));
        return new DownloadCorrespondenceAttachmentResponse(){
            FileName = attachment.FileName,
            Stream = attachmentStream
        };
    }
}
