using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.GetAttachmentOverview;

public class GetAttachmentOverviewHandler(
    IAltinnAuthorizationService altinnAuthorizationService,
    IAttachmentRepository attachmentRepository,
    ICorrespondenceRepository correspondenceRepository,
    ILogger<GetAttachmentOverviewHandler> logger) : IHandler<Guid, GetAttachmentOverviewResponse>
{
    public async Task<OneOf<GetAttachmentOverviewResponse, Error>> Process(Guid attachmentId, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing attachment overview request for {AttachmentId}", attachmentId);
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
        var attachmentStatus = attachment.GetLatestStatus();
        logger.LogDebug("Retrieving correspondence IDs for attachment {AttachmentId}", attachmentId);
        var correspondenceIds = await correspondenceRepository.GetCorrespondenceIdsByAttachmentId(attachmentId, cancellationToken);
        var response = new GetAttachmentOverviewResponse
        {
            AttachmentId = attachment.Id,
            ResourceId = attachment.ResourceId,
            Checksum = attachment.Checksum,
            Status = attachmentStatus.Status,
            StatusText = attachmentStatus.StatusText,
            StatusChanged = attachmentStatus.StatusChanged,
            DataLocationType = attachment.DataLocationType,
            SendersReference = attachment.SendersReference,
            CorrespondenceIds = correspondenceIds,
            FileName = attachment.FileName,
            DisplayName = attachment.DisplayName,
            Sender = attachment.Sender,
        };
        logger.LogInformation("Successfully retrieved overview for attachment {AttachmentId} with status {Status}", 
            attachmentId, 
            attachmentStatus.Status);
        return response;
    }
}
