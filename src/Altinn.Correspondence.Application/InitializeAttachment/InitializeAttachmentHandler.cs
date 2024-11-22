using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.InitializeAttachment;

public class InitializeAttachmentHandler(
    IAttachmentRepository attachmentRepository,
    IAttachmentStatusRepository attachmentStatusRepository,
    IEventBus eventBus,
    IAltinnAuthorizationService altinnAuthorizationService,
    UserClaimsHelper userClaimsHelper,
    ILogger<InitializeAttachmentHandler> logger) : IHandler<InitializeAttachmentRequest, Guid>
{
    private readonly IAttachmentRepository _attachmentRepository = attachmentRepository;
    private readonly IAttachmentStatusRepository _attachmentStatusRepository = attachmentStatusRepository;
    private readonly IEventBus _eventBus = eventBus;
    private readonly IAltinnAuthorizationService _altinnAuthorizationService = altinnAuthorizationService;
    private readonly UserClaimsHelper _userClaimsHelper = userClaimsHelper;
    private readonly ILogger<InitializeAttachmentHandler> _logger = logger;

    public async Task<OneOf<Guid, Error>> Process(InitializeAttachmentRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var hasAccess = await _altinnAuthorizationService.CheckUserAccess(user, request.Attachment.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Write }, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }

        if (!_userClaimsHelper.IsSender(request.Attachment.Sender))
        {
            return Errors.InvalidSender;
        }

        return await TransactionWithRetriesPolicy.Execute(async (cancellationToken) =>
        {
            var attachment = await _attachmentRepository.InitializeAttachment(request.Attachment, cancellationToken);
            await _attachmentStatusRepository.AddAttachmentStatus(new AttachmentStatusEntity
            {
                AttachmentId = attachment.Id,
                StatusChanged = DateTimeOffset.UtcNow,
                Status = AttachmentStatus.Initialized,
                StatusText = AttachmentStatus.Initialized.ToString()
            }, cancellationToken);
            await _eventBus.Publish(AltinnEventType.AttachmentInitialized, attachment.ResourceId, attachment.Id.ToString(), "attachment", attachment.Sender, cancellationToken);

            return attachment.Id;
        }, _logger, cancellationToken);
    }
}