using System.Security.Claims;
using Altinn.Correspondence.Application.Helpers;
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
    private readonly IAltinnAuthorizationService _altinnAuthorizationService = altinnAuthorizationService;
    private readonly IStorageRepository _storageRepository = storageRepository;
    private readonly IAttachmentRepository _attachmentRepository = attachmentRepository;
    private readonly UserClaimsHelper _userClaimsHelper = userClaimsHelper;

    public async Task<OneOf<Stream, Error>> Process(DownloadAttachmentRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var attachment = await _attachmentRepository.GetAttachmentById(request.AttachmentId, false, cancellationToken);
        if (attachment is null)
        {
            return Errors.AttachmentNotFound;
        }
        var hasAccess = await _altinnAuthorizationService.CheckUserAccess(user, attachment.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Write }, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }

        if (!_userClaimsHelper.IsSender(attachment.Sender))
        {
            return Errors.InvalidSender;
        }

        var attachmentStream = await _storageRepository.DownloadAttachment(attachment.Id, cancellationToken);
        return attachmentStream;
    }
}
