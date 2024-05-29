using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using OneOf;

namespace Altinn.Correspondence.Application.UploadAttachmentCommand;

public class UploadAttachmentCommandHandler(IAttachmentRepository attachmentRepository, IStorageRepository storageRepository) : IHandler<UploadAttachmentCommandRequest, UploadAttachmentCommandResponse>
{
    private readonly IAttachmentRepository _attachmentRepository = attachmentRepository;
    private readonly IStorageRepository _storageRepository = storageRepository;

    public async Task<OneOf<UploadAttachmentCommandResponse, Error>> Process(UploadAttachmentCommandRequest request, CancellationToken cancellationToken)
    {
        var attachment = await _attachmentRepository.GetAttachmentById(request.AttachmentId, true, cancellationToken);
        if (attachment == null)
        {
            return Errors.AttachmentNotFound;
        }
        var maxUploadSize = long.Parse(int.MaxValue.ToString());
        if (request.ContentLength > maxUploadSize || request.ContentLength == 0)
        {
            return Errors.InvalidFileSize;
        }

        var statusUpdated = await _attachmentRepository.UpdateAttachmentStatus(request.AttachmentId, AttachmentStatus.UploadProcessing, cancellationToken); // TODO, with malware scan this should be set after upload
        if (statusUpdated is null)
        {
            return Errors.UploadFailed;
        }
        var uploadedFileHash = await _storageRepository.UploadAttachment(request.AttachmentId, request.UploadStream, cancellationToken);
        if (uploadedFileHash is null)
        {
            return Errors.UploadFailed;
        }
        // TODO: will be set by malware scan
        var publishedState = await _attachmentRepository.UpdateAttachmentStatus(request.AttachmentId, AttachmentStatus.Published, cancellationToken);
        if (publishedState is null)
        {
            return Errors.UploadFailed;
        }

        return new UploadAttachmentCommandResponse()
        {
            AttachmentId = attachment.Id,
            Status = publishedState.Status,
            StatusChanged = publishedState.StatusChanged,
            StatusText = publishedState.StatusText
        };
    }
}
