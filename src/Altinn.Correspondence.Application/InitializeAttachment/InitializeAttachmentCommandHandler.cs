using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.InitializeAttachment;

public class InitializeAttachmentHandler : IHandler<InitializeAttachmentRequest, Guid>
{
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly IAttachmentStatusRepository _attachmentStatusRepository;
    private readonly IEventBus _eventBus;
    private readonly IAltinnAuthorizationService _altinnAuthorizationService;
    private readonly UserClaimsHelper _userClaimsHelper;

    public InitializeAttachmentHandler(IAttachmentRepository attachmentRepository, IAttachmentStatusRepository attachmentStatusRepository, IEventBus eventBus, IAltinnAuthorizationService altinnAuthorizationService, UserClaimsHelper userClaimsHelper)
    {
        _attachmentRepository = attachmentRepository;
        _attachmentStatusRepository = attachmentStatusRepository;
        _eventBus = eventBus;
        _altinnAuthorizationService = altinnAuthorizationService;
        _userClaimsHelper = userClaimsHelper;
    }

    public async Task<OneOf<Guid, Error>> Process(InitializeAttachmentRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var hasAccess = await _altinnAuthorizationService.CheckUserAccess(user, request.Attachment.ResourceId, request.Attachment.Sender, null, new List<ResourceAccessLevel> { ResourceAccessLevel.Write }, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }

        if (!_userClaimsHelper.IsSender(request.Attachment.Sender))
        {
            return Errors.InvalidSender;
        }
        var attachment = await _attachmentRepository.InitializeAttachment(request.Attachment, cancellationToken);
        var status = new AttachmentStatusEntity
        {
            AttachmentId = attachment.Id,
            StatusChanged = DateTimeOffset.UtcNow,
            Status = AttachmentStatus.Initialized,
            StatusText = AttachmentStatus.Initialized.ToString()
        };
        await _attachmentStatusRepository.AddAttachmentStatus(status, cancellationToken);
        await _eventBus.Publish(AltinnEventType.AttachmentInitialized, attachment.ResourceId, attachment.Id.ToString(), "attachment", attachment.Sender, cancellationToken);
        return attachment.Id;
    }
}
