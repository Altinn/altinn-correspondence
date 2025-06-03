using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;
using Altinn.Correspondence.Common.Helpers;

namespace Altinn.Correspondence.Application.GetAttachmentDetails;

public class GetAttachmentDetailsHandler(
    IAttachmentRepository attachmentRepository,
    ICorrespondenceRepository correspondenceRepository,
    IAltinnAuthorizationService altinnAuthorizationService,
    ILogger<GetAttachmentDetailsHandler> logger) : IHandler<Guid, GetAttachmentDetailsResponse>
{
    public async Task<OneOf<GetAttachmentDetailsResponse, Error>> Process(Guid attachmentId, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing attachment details request for {AttachmentId}", attachmentId);
        logger.LogDebug("Retrieving attachment {AttachmentId} with status history", attachmentId);
        var attachment = await attachmentRepository.GetAttachmentById(attachmentId, true, cancellationToken);
        if (attachment == null)
        {
            logger.LogWarning("Attachment {AttachmentId} not found", attachmentId);
            return AttachmentErrors.AttachmentNotFound;
        }
        logger.LogDebug("Checking sender access for attachment {AttachmentId} and resource {ResourceId}", 
            attachmentId, 
            attachment.ResourceId);
        var hasAccess = await altinnAuthorizationService.CheckAccessAsSender(
            user,
            attachment.ResourceId,
            attachment.Sender.WithoutPrefix(),
            attachment.Id.ToString(),
            cancellationToken);
        if (!hasAccess)
        {
            logger.LogWarning("Access denied for attachment {AttachmentId} - user does not have sender access", attachmentId);
            return AuthorizationErrors.NoAccessToResource;
        }
        logger.LogDebug("Retrieving correspondence IDs for attachment {AttachmentId}", attachmentId);
        var correspondenceIds = await correspondenceRepository.GetCorrespondenceIdsByAttachmentId(attachmentId, cancellationToken);
        var attachmentStatus = attachment.GetLatestStatus();

        logger.LogDebug("Determining content type for file {FileName}", attachment.FileName);
        var fileName = attachment.FileName;
        var contentType = FileConstants.GetMIMEType(fileName);

        logger.LogDebug("Building response for attachment {AttachmentId}", attachmentId);
        var response = new GetAttachmentDetailsResponse
        {
            ResourceId = attachment.ResourceId,
            AttachmentId = attachment.Id,
            Status = attachmentStatus.Status,
            Statuses = attachment.Statuses,
            StatusText = attachmentStatus.StatusText,
            StatusChanged = attachmentStatus.StatusChanged,
            DataLocationType = attachment.DataLocationType,
            DataType = contentType,
            SendersReference = attachment.SendersReference,
            CorrespondenceIds = correspondenceIds,
            FileName = attachment.FileName,
            DisplayName = attachment.DisplayName,
            Sender = attachment.Sender,
            IsEncrypted = attachment.IsEncrypted,
            Checksum = attachment.Checksum,
        };
        logger.LogInformation("Successfully retrieved details for attachment {AttachmentId} with status {Status}", 
            attachmentId, 
            attachmentStatus.Status);
        return response;
    }
}
