using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Repositories;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.DownloadAttachment;

public class DownloadAttachmentHandler(
    IAltinnAuthorizationService altinnAuthorizationService,
    IStorageRepository storageRepository,
    IAttachmentRepository attachmentRepository) : IHandler<DownloadAttachmentRequest, Stream>
{
    public async Task<OneOf<Stream, Error>> Process(DownloadAttachmentRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var attachment = await attachmentRepository.GetAttachmentById(request.AttachmentId, false, cancellationToken);
        if (attachment is null)
        {
            return AttachmentErrors.AttachmentNotFound;
        }
        var hasAccess = await altinnAuthorizationService.CheckAccessAsSender(user, attachment.ResourceId, attachment.Sender.WithoutPrefix(), null, cancellationToken);
        if (!hasAccess)
        {
            return AuthorizationErrors.NoAccessToResource;
        }
        var attachmentStream = await storageRepository.DownloadAttachment(attachment.Id, attachment.StorageProvider, cancellationToken);
        return attachmentStream;
    }
}
