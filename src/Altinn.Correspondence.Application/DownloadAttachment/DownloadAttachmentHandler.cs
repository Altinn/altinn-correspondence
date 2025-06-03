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
    ILogger<DownloadAttachmentHandler> logger) : IHandler<DownloadAttachmentRequest, Stream>
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
        logger.LogDebug("Checking sender access for attachment {AttachmentId} and resource {ResourceId}", 
            request.AttachmentId, 
            attachment.ResourceId);
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
        logger.LogDebug("Downloading attachment {AttachmentId} from storage provider {StorageProvider}", 
            request.AttachmentId, 
            attachment.StorageProvider);
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
