using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
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
        var hasAccess = await _altinnAuthorizationService.CheckUserAccess(attachment.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Open }, cancellationToken);
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
        var uploadResult = await uploadHelper.UploadAttachment(request.UploadStream, request.AttachmentId, cancellationToken);

        await uploadHelper.CheckCorrespondenceStatusesAfterUploadAndPublish(attachment.Id, cancellationToken);

        return uploadResult.Match<OneOf<UploadAttachmentResponse, Error>>(
            data => { return data; },
            error => { return error; }
        );
    }
}
