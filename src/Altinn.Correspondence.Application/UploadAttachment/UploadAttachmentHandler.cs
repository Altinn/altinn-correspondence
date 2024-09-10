using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Microsoft.Extensions.Hosting;
using OneOf;

namespace Altinn.Correspondence.Application.UploadAttachment;

public class UploadAttachmentHandler(IAltinnAuthorizationService altinnAuthorizationService, IAttachmentRepository attachmentRepository, IAttachmentStatusRepository attachmentStatusRepository, IStorageRepository storageRepository, ICorrespondenceRepository correspondenceRepository, ICorrespondenceStatusRepository correspondenceStatusRepository, IHostEnvironment hostEnvironment, IEventBus eventBus) : IHandler<UploadAttachmentRequest, UploadAttachmentResponse>
{
    private readonly IAltinnAuthorizationService _altinnAuthorizationService = altinnAuthorizationService;
    private readonly IAttachmentRepository _attachmentRepository = attachmentRepository;
    private readonly IAttachmentStatusRepository _attachmentStatusRepository = attachmentStatusRepository;
    private readonly ICorrespondenceRepository _correspondenceRepository = correspondenceRepository;
    private readonly ICorrespondenceStatusRepository _correspondenceStatusRepository = correspondenceStatusRepository;
    private readonly IStorageRepository _storageRepository = storageRepository;
    private readonly IHostEnvironment _hostEnvironment = hostEnvironment;
    private readonly IEventBus _eventBus = eventBus;

    public async Task<OneOf<UploadAttachmentResponse, Error>> Process(UploadAttachmentRequest request, CancellationToken cancellationToken)
    {
        var attachment = await _attachmentRepository.GetAttachmentById(request.AttachmentId, true, cancellationToken);
        if (attachment == null)
        {
            return Errors.AttachmentNotFound;
        }
        var hasAccess = await _altinnAuthorizationService.CheckUserAccess(attachment.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Send }, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
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
        UploadHelper uploadHelper = new UploadHelper(_correspondenceRepository, _correspondenceStatusRepository, _attachmentStatusRepository, _attachmentRepository, _storageRepository, _hostEnvironment);

        var errorsBeforeUpload = await uploadHelper.IsCorrespondenceNotInitialized(request.AttachmentId);
        if (errorsBeforeUpload != null)
        {
            return errorsBeforeUpload;
        }
        var uploadResult = await uploadHelper.UploadAttachment(request.UploadStream, request.AttachmentId, cancellationToken);

        var errorsAfterUpload = await uploadHelper.IsCorrespondenceNotInitialized(request.AttachmentId); // After upload, check if Correspondence status has changed
        if (errorsAfterUpload != null)
        {
            await _storageRepository.PurgeAttachment(request.AttachmentId, cancellationToken);
            await _attachmentStatusRepository.AddAttachmentStatus(new AttachmentStatusEntity
            {
                AttachmentId = request.AttachmentId,
                Status = AttachmentStatus.Purged,
                StatusChanged = DateTimeOffset.UtcNow,
                StatusText = AttachmentStatus.Purged.ToString()
            }, cancellationToken);

            await _eventBus.Publish(AltinnEventType.AttachmentPurged, attachment.ResourceId, request.AttachmentId.ToString(), "attachment", attachment.Sender, cancellationToken);
            return Errors.CorrespondenceFailedDuringUpload;
        }

        if (_hostEnvironment.IsDevelopment())
        {
            await uploadHelper.CheckCorrespondenceStatusesAfterUploadAndPublish(attachment.Id, true, cancellationToken);
        }

        return uploadResult.Match<OneOf<UploadAttachmentResponse, Error>>(
            data => { return data; },
            error => { return error; }
        );
    }
}
