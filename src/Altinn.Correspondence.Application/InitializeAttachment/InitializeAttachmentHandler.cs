using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Altinn.Correspondence.Integrations.Altinn.ResourceRegistry;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.InitializeAttachment;

public class InitializeAttachmentHandler(
    IAltinnRegisterService altinnRegisterService,
    IAttachmentRepository attachmentRepository,
    IAttachmentStatusRepository attachmentStatusRepository,
    IResourceRegistryService resourceRegistryService,
    IEventBus eventBus,
    IAltinnAuthorizationService altinnAuthorizationService,
    ILogger<InitializeAttachmentHandler> logger,
    AttachmentHelper attachmentHelper) : IHandler<InitializeAttachmentRequest, Guid>
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
            return AuthorizationErrors.NoAccessToResource;
        }
        var resourceType = await resourceRegistryService.GetResourceType(request.Attachment.ResourceId, cancellationToken);
        if (resourceType is null)
        {
            throw new Exception($"Resource type not found for {request.Attachment.ResourceId}. This should be impossible as authorization worked.");
        }
        if (resourceType != "GenericAccessResource" && resourceType != "CorrespondenceService")
        {
            return AuthorizationErrors.IncorrectResourceType;
        }

        var party = await altinnRegisterService.LookUpPartyById(user.GetCallerOrganizationId(), cancellationToken);
        if (party?.PartyUuid is not Guid partyUuid)
        {
            return AuthorizationErrors.CouldNotFindPartyUuid;
        }
        var attachment = request.Attachment;
        var attachmentNameError = attachmentHelper.ValidateAttachmentName(request.Attachment);
        if (attachmentNameError is not null)
        {
            return attachmentNameError;
        }
        if (attachment.Sender.StartsWith("0192:"))
        {
            attachment.Sender = $"{UrnConstants.OrganizationNumberAttribute}:{attachment.Sender.WithoutPrefix()}";
            logger.LogInformation($"'0192:' prefix detected for sender in initialization of attachment. Replacing prefix with {UrnConstants.OrganizationNumberAttribute}.");
        }
        return await TransactionWithRetriesPolicy.Execute<Guid>(async (cancellationToken) =>
        {
            var initializedAttachment = await attachmentRepository.InitializeAttachment(attachment, cancellationToken);
            await attachmentHelper.SetAttachmentStatus(initializedAttachment.Id, AttachmentStatus.Initialized, partyUuid, cancellationToken);
            await eventBus.Publish(AltinnEventType.AttachmentInitialized, initializedAttachment.ResourceId, initializedAttachment.Id.ToString(), "attachment", initializedAttachment.Sender, cancellationToken);

            return initializedAttachment.Id;
        }, logger, cancellationToken);
    }

}