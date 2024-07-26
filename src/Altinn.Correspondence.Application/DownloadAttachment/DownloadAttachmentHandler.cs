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
        var attachmentId = await _correspondenceAttachmentRepository.GetAttachmentIdByCorrespondenceAttachmentId(request.AttachmentId, true, cancellationToken);
        if (attachmentId is null || attachmentId == Guid.Empty)
        {
            return Errors.AttachmentNotFound;
        }
        var attachment = await _attachmentRepository.GetAttachmentById((Guid)attachmentId, false, cancellationToken);
        var hasAccess = await _altinnAuthorizationService.CheckUserAccess(attachment.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Open }, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }

        var attachmentStream = await _storageRepository.DownloadAttachment((Guid)attachmentId, cancellationToken);
        return attachmentStream;
    }
}
