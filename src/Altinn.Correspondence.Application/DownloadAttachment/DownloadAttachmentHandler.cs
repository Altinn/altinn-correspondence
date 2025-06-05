using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.DownloadAttachment;

public class DownloadAttachmentHandler(
    IAltinnAuthorizationService altinnAuthorizationService,
    IStorageRepository storageRepository,
    IAttachmentRepository attachmentRepository,
    ILogger<DownloadAttachmentHandler> logger,
    ICorrespondenceRepository correspondenceRepository) : IHandler<DownloadAttachmentRequest, Stream>
{
    public async Task<OneOf<Stream, Error>> Process(DownloadAttachmentRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing download request for attachment {AttachmentId}", request.AttachmentId);
        var attachment = await attachmentRepository.GetAttachmentById(request.AttachmentId, false, cancellationToken);
        if (attachment is null)
        {
            logger.LogWarning("Attachment {AttachmentId} not found", request.AttachmentId);
            return AttachmentErrors.AttachmentNotFound;
        }
        var hasAccess = await altinnAuthorizationService.CheckAccessAsSender(
            user, 
            attachment.ResourceId, 
            attachment.Sender.WithoutPrefix(), 
            null, 
            cancellationToken);
        if (!hasAccess)
        {
            logger.LogWarning("Access denied for attachment {AttachmentId} - user does not have sender access", request.AttachmentId);
            return AuthorizationErrors.NoAccessToResource;
        }
        var associatedCorrespondences = await correspondenceRepository.GetCorrespondencesByAttachmentId(attachment.Id, true, cancellationToken);
        foreach(var correspondence in associatedCorrespondences)
        {
            if (correspondence.StatusHasBeen(Core.Models.Enums.CorrespondenceStatus.Published))
            {
                return AttachmentErrors.AttachedToAPublishedCorrespondence;
            }
        }        
        var attachmentStream = await storageRepository.DownloadAttachment(
            attachment.Id, 
            attachment.StorageProvider, 
            cancellationToken);
        logger.LogInformation("Successfully downloaded attachment {AttachmentId} with filename {FileName}", 
            request.AttachmentId, 
            attachment.FileName);
        return attachmentStream;
    }
}
