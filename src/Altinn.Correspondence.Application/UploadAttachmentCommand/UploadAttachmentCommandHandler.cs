using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Microsoft.Extensions.Hosting;
using OneOf;

namespace Altinn.Correspondence.Application.UploadAttachmentCommand;

public class UploadAttachmentCommandHandler(IAttachmentRepository attachmentRepository, IAttachmentStatusRepository attachmentStatusRepository, IStorageRepository storageRepository, ICorrespondenceRepository correspondenceRepository, ICorrespondenceStatusRepository correspondenceStatusRepository, IHostEnvironment hostEnvironment, IEventBus eventBus) : IHandler<UploadAttachmentCommandRequest, UploadAttachmentCommandResponse>
{
    private readonly IAttachmentRepository _attachmentRepository = attachmentRepository;
    private readonly IAttachmentStatusRepository _attachmentStatusRepository = attachmentStatusRepository;
    private readonly ICorrespondenceRepository _correspondenceRepository = correspondenceRepository;
    private readonly ICorrespondenceStatusRepository _correspondenceStatusRepository = correspondenceStatusRepository;
    private readonly IStorageRepository _storageRepository = storageRepository;
    private readonly IHostEnvironment _hostEnvironment = hostEnvironment;
    private readonly IEventBus _eventBus = eventBus;

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
            return Errors.InvalidUploadAttachmentStatus;
        }

        var currentStatus = new AttachmentStatusEntity
        {
            AttachmentId = request.AttachmentId,
            Status = AttachmentStatus.UploadProcessing,
            StatusChanged = DateTimeOffset.UtcNow,
            StatusText = AttachmentStatus.UploadProcessing.ToString()
        };
        await _attachmentStatusRepository.AddAttachmentStatus(currentStatus, cancellationToken); // TODO, with malware scan this should be set after upload
        var dataLocationUrl = await _storageRepository.UploadAttachment(request.AttachmentId, request.UploadStream, cancellationToken);
        if (dataLocationUrl is null)
        {
            currentStatus = new AttachmentStatusEntity
            {
                AttachmentId = request.AttachmentId,
                Status = AttachmentStatus.Failed,
                StatusChanged = DateTimeOffset.UtcNow,
                StatusText = AttachmentStatus.Failed.ToString()
            };
            await _attachmentStatusRepository.AddAttachmentStatus(currentStatus, cancellationToken);
            return Errors.UploadFailed;
        }
        await _attachmentRepository.SetDataLocationUrl(attachment, AttachmentDataLocationType.AltinnCorrespondenceAttachment, dataLocationUrl, cancellationToken);
        if (_hostEnvironment.IsDevelopment()) // No malware scan when running locally
        {
            currentStatus = new AttachmentStatusEntity
            {
                AttachmentId = request.AttachmentId,
                Status = AttachmentStatus.Published,
                StatusChanged = DateTimeOffset.UtcNow,
                StatusText = AttachmentStatus.Published.ToString()
            };
            await _attachmentStatusRepository.AddAttachmentStatus(currentStatus, cancellationToken);
        }
        await CheckCorrespondenceStatusesAfterUploadAndPublish(attachment.Id, cancellationToken);

        await _eventBus.Publish(AltinnEventType.AttachmentPublished, null, request.AttachmentId.ToString(), "attachment", null, cancellationToken);
        return new UploadAttachmentCommandResponse()
        {
            AttachmentId = attachment.Id,
            Status = currentStatus.Status,
            StatusChanged = currentStatus.StatusChanged,
            StatusText = currentStatus.StatusText
        };
    }

    public async Task CheckCorrespondenceStatusesAfterUploadAndPublish(Guid attachmentId, CancellationToken cancellationToken)
    {
        var attachment = await _attachmentRepository.GetAttachmentById(attachmentId, true, cancellationToken);
        if (attachment == null)
        {
            return;
        }

        var correspondences = await _correspondenceRepository.GetNonPublishedCorrespondencesByAttachmentId(attachment.Id, AttachmentStatus.Published, cancellationToken);
        if (correspondences.Count == 0)
        {
            return;
        }

        var list = new List<CorrespondenceStatusEntity>();
        foreach (var correspondence in correspondences)
        {
            list.Add(
                new CorrespondenceStatusEntity
                {
                    CorrespondenceId = correspondence.Id,
                    Status = CorrespondenceStatus.Published,
                    StatusChanged = DateTime.UtcNow,
                    StatusText = CorrespondenceStatus.Published.ToString()
                }
            );
        }
        await _correspondenceStatusRepository.AddCorrespondenceStatuses(list, cancellationToken);
        return;
    }
}
