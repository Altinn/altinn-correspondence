using System.Security.Claims;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Application.ForwardCorrespondence;

public class ForwardingAttachmentAccessHelper(
    IAltinnAuthorizationService altinnAuthorizationService,
    IAttachmentRepository attachmentRepository,
    ILogger<ForwardingAttachmentAccessHelper> logger
)
{
    private const long MaxTotalAttachmentSize = 10_000_000; // 10 MB

    /// <summary>
    /// Returns the constraint that blocks forwarding the correspondence with attachments, or None if it can be forwarded with attachments.
    /// </summary>
    public async Task<ForwardingConstraints> GetForwardingConstraints(
        CorrespondenceEntity correspondence,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        var inaccessibleAttachment = await GetFirstInaccessibleAttachment(user, correspondence, cancellationToken);
        if (inaccessibleAttachment is not null)
        {
            logger.LogInformation("User lacks access to attachment resource {ResourceId} on correspondence {CorrespondenceId}", inaccessibleAttachment.ResourceId, correspondence.Id);
            return ForwardingConstraints.NotAccessToAllAttachments;
        }
        if (HasExpiringAttachments(correspondence))
        {
            logger.LogInformation("Correspondence {CorrespondenceId} has expiring attachments", correspondence.Id);
            return ForwardingConstraints.ExpiringAttachments;
        }
        var totalAttachmentSize = await attachmentRepository.GetTotalAttachmentSizeByCorrespondence(correspondence.Id, cancellationToken);
        if (totalAttachmentSize > MaxTotalAttachmentSize)
        {
            logger.LogInformation("Correspondence {CorrespondenceId} has attachments exceeding 10 MB", correspondence.Id);
            return ForwardingConstraints.AttachmentsExceedMaxLimit;
        }
        return ForwardingConstraints.None;
    }

    /// <summary>
    /// Returns the first attachment the user lacks access to, or null if the user has access to all attachments.
    /// Attachments can belong to other resources than the correspondence, so access is checked once per distinct attachment resource.
    /// The correspondence's own resource is skipped, as access to it is checked separately.
    /// </summary>
    private async Task<AttachmentEntity?> GetFirstInaccessibleAttachment(
        ClaimsPrincipal user,
        CorrespondenceEntity correspondence,
        CancellationToken cancellationToken)
    {
        var attachmentsWithOtherResources = correspondence.Content.Attachments
            .Select(correspondenceAttachment => correspondenceAttachment.Attachment)
            .OfType<AttachmentEntity>()
            .Where(attachment => attachment.ResourceId != correspondence.ResourceId)
            .DistinctBy(attachment => attachment.ResourceId);
        foreach (var attachment in attachmentsWithOtherResources)
        {
            if (!await altinnAuthorizationService.CheckAttachmentAccessAsRecipient(user, correspondence, attachment, cancellationToken))
            {
                return attachment;
            }
        }
        return null;
    }

    /// <summary>
    /// Expiring attachments are not forwarded, as the recipient would keep a copy after the attachment has expired in Altinn.
    /// </summary>
    private static bool HasExpiringAttachments(CorrespondenceEntity correspondence)
        => correspondence.Content?.Attachments.Any(attachment => attachment.ExpirationTime is not null) ?? false;
}
