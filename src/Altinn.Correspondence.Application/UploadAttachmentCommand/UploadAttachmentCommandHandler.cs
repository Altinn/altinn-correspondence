using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using OneOf;

namespace Altinn.Correspondence.Application.UploadAttachmentCommand;

public class UploadAttachmentCommandHandler(IAttachmentRepository attachmentRepository, IAttachmentStatusRepository attachmentStatusRepository, IStorageRepository storageRepository) : IHandler<UploadAttachmentCommandRequest, UploadAttachmentCommandResponse>
{
    private readonly IAttachmentRepository _attachmentRepository = attachmentRepository;
    private readonly IAttachmentStatusRepository _attachmentStatusRepository = attachmentStatusRepository;
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
        if (attachment.Statuses.Any(status => status.Status == AttachmentStatus.UploadProcessing))
        {
            return Errors.InvalidAttachmentStatus;
        }

        await _attachmentStatusRepository.AddAttachmentStatus(new AttachmentStatusEntity
        {
            AttachmentId = request.AttachmentId,
            Status = AttachmentStatus.UploadProcessing,
            StatusChanged = DateTime.UtcNow,
            StatusText = AttachmentStatus.UploadProcessing.ToString()
        }, cancellationToken); // TODO, with malware scan this should be set after upload
        var uploadedFileHash = await _storageRepository.UploadAttachment(request.AttachmentId, request.UploadStream, cancellationToken);
        if (uploadedFileHash is null)
        {
            await _attachmentStatusRepository.AddAttachmentStatus(new AttachmentStatusEntity
            {
                AttachmentId = request.AttachmentId,
                Status = AttachmentStatus.Failed,
                StatusChanged = DateTime.UtcNow,
                StatusText = AttachmentStatus.Failed.ToString()
            }, cancellationToken);
            return Errors.UploadFailed;
        }
        // TODO: will be set by malware scan
        var publishStatus = new AttachmentStatusEntity
        {
            AttachmentId = request.AttachmentId,
            Status = AttachmentStatus.Published,
            StatusChanged = DateTime.UtcNow,
            StatusText = AttachmentStatus.Published.ToString()
        };
        await _attachmentStatusRepository.AddAttachmentStatus(publishStatus, cancellationToken);

        return new UploadAttachmentCommandResponse()
        {
            AttachmentId = attachment.Id,
            Status = publishStatus.Status,
            StatusChanged = publishStatus.StatusChanged,
            StatusText = publishStatus.StatusText
        };
    }
}
