using System.Security.Claims;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Repositories;

namespace Altinn.Correspondence.Application.ForwardCorrespondence;

public static class ForwardingAttachmentAccessHelper
{
    /// <summary>
    /// Returns the first attachment the user lacks access to, or null if the user has access to all attachments.
    /// Attachments can belong to other resources than the correspondence, so access is checked once per distinct attachment resource.
    /// The correspondence's own resource is skipped, as access to it is checked separately.
    /// </summary>
    public static async Task<AttachmentEntity?> GetFirstInaccessibleAttachment(
        this IAltinnAuthorizationService altinnAuthorizationService,
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
}
