using System.Security.Claims;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using OneOf;

namespace Altinn.Correspondence.Application.DownloadAttachment;

public class DownloadAttachmentHandler(
    IAltinnAuthorizationService altinnAuthorizationService,
    IStorageRepository storageRepository,
    IAttachmentRepository attachmentRepository,
    UserClaimsHelper userClaimsHelper) : IHandler<DownloadAttachmentRequest, Stream>
{
    public async Task<OneOf<Stream, Error>> Process(DownloadAttachmentRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var attachment = await attachmentRepository.GetAttachmentById(request.AttachmentId, false, cancellationToken);
        if (attachment is null)
        {
            return Errors.AttachmentNotFound;
        }
        var hasAccess = await altinnAuthorizationService.CheckUserAccess(
            user,
            attachment.ResourceId,
            attachment.Sender.WithoutPrefix(),
            attachment.Id.ToString(),
            new List<ResourceAccessLevel> { ResourceAccessLevel.Write },
            cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }
        var attachmentStream = await storageRepository.DownloadAttachment(attachment.Id, cancellationToken);
        return attachmentStream;
    }
}
