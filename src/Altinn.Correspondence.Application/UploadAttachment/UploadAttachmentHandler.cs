using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Microsoft.Extensions.Hosting;
using OneOf;

namespace Altinn.Correspondence.Application.UploadAttachment;

public class UploadAttachmentHandler(IAltinnAuthorizationService altinnAuthorizationService, IAttachmentRepository attachmentRepository, ICorrespondenceRepository correspondenceRepository, UploadHelper uploadHelper) : IHandler<UploadAttachmentRequest, UploadAttachmentResponse>
{
    private readonly IAltinnAuthorizationService _altinnAuthorizationService = altinnAuthorizationService;
    private readonly IAttachmentRepository _attachmentRepository = attachmentRepository;
    private readonly ICorrespondenceRepository _correspondenceRepository = correspondenceRepository;
    private readonly UploadHelper _uploadHelper = uploadHelper;

    public async Task<OneOf<UploadAttachmentResponse, Error>> Process(UploadAttachmentRequest request, CancellationToken cancellationToken)
    {
        var attachment = await _attachmentRepository.GetAttachmentById(request.AttachmentId, true, cancellationToken);
        if (attachment == null)
        {
            return Errors.AttachmentNotFound;
        }
        var hasAccess = await _altinnAuthorizationService.CheckUserAccess(attachment.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Write }, cancellationToken);
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

        // Check if any correspondences are attached. 
        var correspondences = await _correspondenceRepository.GetCorrespondencesByAttachmentId(request.AttachmentId, false);
        if (correspondences.Count != 0)
        {
            return Errors.CantUploadToExistingCorrespondence;
        }

        var uploadResult = await _uploadHelper.UploadAttachment(request.UploadStream, request.AttachmentId, cancellationToken);

        return uploadResult.Match<OneOf<UploadAttachmentResponse, Error>>(
            data => { return data; },
            error => { return error; }
        );
    }
}
