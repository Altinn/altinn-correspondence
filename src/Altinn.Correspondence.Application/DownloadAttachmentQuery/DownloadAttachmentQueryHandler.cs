using Altinn.Correspondence.Core.Repositories;
using OneOf;

namespace Altinn.Correspondence.Application.DownloadAttachmentQuery
{
    public class DownloadAttachmentQueryHandler : IHandler<DownloadAttachmentQueryRequest, Stream>
    {
        private readonly IAttachmentRepository _attachmentRepository;
        private readonly IStorageRepository _storageRepository;
        public DownloadAttachmentQueryHandler(IAttachmentRepository attachmentRepository, IStorageRepository storageRepository)
        {
            _attachmentRepository = attachmentRepository;
            _storageRepository = storageRepository;
        }

        public async Task<OneOf<Stream, Error>> Process(DownloadAttachmentQueryRequest request, CancellationToken cancellationToken)
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
