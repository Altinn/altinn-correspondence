using Altinn.Correspondence.Core.Repositories;
using OneOf;

namespace Altinn.Correspondence.Application.DownloadAttachment;

public class DownloadAttachmentHandler : IHandler<DownloadAttachmentRequest, Stream>
{
    private readonly IStorageRepository _storageRepository;
    private readonly ICorrespondenceAttachmentRepository _correspondenceAttachmentRepository;

    public DownloadAttachmentHandler(IStorageRepository storageRepository, ICorrespondenceAttachmentRepository correspondenceAttachmentRepository)
    {
        _storageRepository = storageRepository;
        _correspondenceAttachmentRepository = correspondenceAttachmentRepository;
    }

    public async Task<OneOf<Stream, Error>> Process(DownloadAttachmentRequest request, CancellationToken cancellationToken)
    {
        var attachmentId = await _correspondenceAttachmentRepository.GetAttachmentIdByCorrespondenceAttachmentId(request.AttachmentId, cancellationToken);
        if (attachmentId is null)
        {
            return Errors.AttachmentNotFound;
        }

        var attachmentStream = await _storageRepository.DownloadAttachment((Guid)attachmentId, cancellationToken);
        return attachmentStream;
    }
}
