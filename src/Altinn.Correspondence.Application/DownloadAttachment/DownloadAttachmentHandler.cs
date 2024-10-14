using System.Runtime.CompilerServices;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using OneOf;

namespace Altinn.Correspondence.Application.DownloadAttachment;

public class DownloadAttachmentHandler : IHandler<DownloadAttachmentRequest, Stream>
{
    private readonly IAltinnAuthorizationService _altinnAuthorizationService;
    private readonly IStorageRepository _storageRepository;
    private readonly IAttachmentRepository _attachmentRepository;

    public DownloadAttachmentHandler(IAltinnAuthorizationService altinnAuthorizationService, IStorageRepository storageRepository, IAttachmentRepository attachmentRepository)
    {
        _altinnAuthorizationService = altinnAuthorizationService;
        _storageRepository = storageRepository;
        _attachmentRepository = attachmentRepository;
    }

    public async Task<OneOf<Stream, Error>> Process(DownloadAttachmentRequest request, CancellationToken cancellationToken)
    {
        var attachment = await _attachmentRepository.GetAttachmentById(request.AttachmentId, false, cancellationToken);
        if (attachment is null)
        {
            return Errors.AttachmentNotFound;
        }
        var hasAccess = await _altinnAuthorizationService.CheckUserAccess(attachment.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Write }, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }

        var attachmentStream = await _storageRepository.DownloadAttachment(attachment.Id, cancellationToken);
        return attachmentStream;
    }
}
