using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.UploadAttachment;

public class UploadAttachmentHandler(
    IAltinnAuthorizationService altinnAuthorizationService,
    IAttachmentRepository attachmentRepository,
    ICorrespondenceRepository correspondenceRepository,
    UploadHelper uploadHelper,
    UserClaimsHelper userClaimsHelper,
    ILogger<UploadAttachmentHandler> logger) : IHandler<UploadAttachmentRequest, UploadAttachmentResponse>
{
    public async Task<OneOf<UploadAttachmentResponse, Error>> Process(UploadAttachmentRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var attachment = await attachmentRepository.GetAttachmentById(request.AttachmentId, true, cancellationToken);
        if (attachment == null)
        {
            return Errors.AttachmentNotFound;
        }
        var hasAccess = await altinnAuthorizationService.CheckAccessAsSender(
            user,
            attachment.ResourceId,
            attachment.Sender.WithoutPrefix(),
            attachment.Id.ToString(),
            cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }
        var maxUploadSize = long.Parse(int.MaxValue.ToString());
        if (request.ContentLength > maxUploadSize || request.ContentLength == 0)
        {
            return Errors.InvalidFileSize;
        }
        if (attachment.StatusHasBeen(AttachmentStatus.UploadProcessing))
        {
            return Errors.InvalidUploadAttachmentStatus;
        }
        // Check if any correspondences are attached. 
        var correspondences = await correspondenceRepository.GetCorrespondencesByAttachmentId(request.AttachmentId, false);
        if (correspondences.Count != 0)
        {
            return Errors.CantUploadToExistingCorrespondence;
        }
        return await TransactionWithRetriesPolicy.Execute(async (cancellationToken) =>
        {
            var uploadResult = await uploadHelper.UploadAttachment(request.UploadStream, request.AttachmentId, cancellationToken);
            return uploadResult.Match<OneOf<UploadAttachmentResponse, Error>>(
                data => { return data; },
                error => { return error; }
            );
        }, logger, cancellationToken);
    }
}
