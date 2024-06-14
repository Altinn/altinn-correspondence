using Altinn.Correspondence.Core.Repositories;
using OneOf;

namespace Altinn.Correspondence.Application.DownloadAttachment
{
    public class DownloadAttachmentHandler : IHandler<DownloadAttachmentRequest, Stream>
    {
        private readonly IAttachmentRepository _attachmentRepository;
        private readonly IStorageRepository _storageRepository;
        public DownloadAttachmentHandler(IAttachmentRepository attachmentRepository, IStorageRepository storageRepository)
        {
            _attachmentRepository = attachmentRepository;
            _storageRepository = storageRepository;
        }

        public async Task<OneOf<Stream, Error>> Process(DownloadAttachmentRequest request, CancellationToken cancellationToken)
        {
            var attachment = await _attachmentRepository.GetAttachmentById(request.AttachmentId, false, cancellationToken);
            if (attachment is null)
            {
                return Errors.AttachmentNotFound;
            }

            var attachmentStream = await _storageRepository.DownloadAttachment(request.AttachmentId, cancellationToken);
            return attachmentStream;
        }
    }
}
