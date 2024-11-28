using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Helpers;
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
    IAltinnRegisterService altinnRegisterService,
    IAttachmentRepository attachmentRepository,
    IAttachmentStatusRepository attachmentStatusRepository,
    IEventBus eventBus,
    IAltinnAuthorizationService altinnAuthorizationService,
    ILogger<InitializeAttachmentHandler> logger) : IHandler<InitializeAttachmentRequest, Guid>
{
    public async Task<OneOf<Guid, Error>> Process(InitializeAttachmentRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var hasAccess = await altinnAuthorizationService.CheckAccessAsSender(
            user,
            request.Attachment.ResourceId,
            request.Attachment.Sender.WithoutPrefix(),
            null,
            cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }

        var party = await altinnRegisterService.LookUpPartyById(user.GetCallerOrganizationId(), cancellationToken);
        if (party?.PartyUuid is not Guid partyUuid)
        {
            return Errors.CouldNotFindPartyUuid;
        }
        return await TransactionWithRetriesPolicy.Execute<Guid>(async (cancellationToken) =>
        {
            var attachment = await attachmentRepository.InitializeAttachment(request.Attachment, cancellationToken);
            await attachmentStatusRepository.AddAttachmentStatus(new AttachmentStatusEntity
            {
                AttachmentId = attachment.Id,
                StatusChanged = DateTimeOffset.UtcNow,
                Status = AttachmentStatus.Initialized,
                StatusText = AttachmentStatus.Initialized.ToString(),
                PartyUuid = partyUuid
            }, cancellationToken);
            await eventBus.Publish(AltinnEventType.AttachmentInitialized, attachment.ResourceId, attachment.Id.ToString(), "attachment", attachment.Sender, cancellationToken);

            return attachment.Id;
        }, logger, cancellationToken);
    }
}