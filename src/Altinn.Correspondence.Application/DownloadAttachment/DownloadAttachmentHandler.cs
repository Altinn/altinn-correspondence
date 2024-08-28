using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using OneOf;

namespace Altinn.Correspondence.Application.DownloadAttachment;

public class DownloadAttachmentHandler : IHandler<DownloadAttachmentRequest, Stream>
{
    private readonly IAltinnAuthorizationService _altinnAuthorizationService;
    private readonly IStorageRepository _storageRepository;
    private readonly ICorrespondenceAttachmentRepository _correspondenceAttachmentRepository;
    private readonly IAttachmentRepository _attachmentRepository;

    public DownloadAttachmentHandler(IAltinnAuthorizationService altinnAuthorizationService, IStorageRepository storageRepository, ICorrespondenceAttachmentRepository correspondenceAttachmentRepository, IAttachmentRepository attachmentRepository)
    {
        _altinnAuthorizationService = altinnAuthorizationService;
        _storageRepository = storageRepository;
        _correspondenceAttachmentRepository = correspondenceAttachmentRepository;
        _attachmentRepository = attachmentRepository;
    }

    public async Task<OneOf<Stream, Error>> Process(DownloadAttachmentRequest request, CancellationToken cancellationToken)
    {
        var attachment = await _attachmentRepository.GetAttachmentByCorrespondenceIdAndAttachmentId(request.CorrespondenceId, request.AttachmentId, cancellationToken);
        if (attachment is null)
        {
            return Errors.AttachmentNotFound;
        }
        var hasAccess = await _altinnAuthorizationService.CheckUserAccess(attachment.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Open }, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }

        var attachmentStream = await _storageRepository.DownloadAttachment((Guid)attachment.Id, cancellationToken);
        return attachmentStream;
    }
}
