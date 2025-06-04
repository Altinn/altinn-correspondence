using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Hangfire;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.InitializeAttachment;

public class InitializeAttachmentHandler(
    IAltinnRegisterService altinnRegisterService,
    IAttachmentRepository attachmentRepository,
    IAttachmentStatusRepository attachmentStatusRepository,
    IResourceRegistryService resourceRegistryService,
    IAltinnAuthorizationService altinnAuthorizationService,
    ILogger<InitializeAttachmentHandler> logger,
    IBackgroundJobClient backgroundJobClient,
    AttachmentHelper attachmentHelper) : IHandler<InitializeAttachmentRequest, Guid>
{
    public async Task<OneOf<Guid, Error>> Process(InitializeAttachmentRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting attachment initialization process for resource {ResourceId}", request.Attachment.ResourceId.SanitizeForLogging());
        var hasAccess = await altinnAuthorizationService.CheckAccessAsSender(
            user,
            request.Attachment.ResourceId,
            request.Attachment.Sender.WithoutPrefix(),
            null,
            cancellationToken);
        if (!hasAccess)
        {
            logger.LogWarning("Access denied for resource {ResourceId} - user does not have sender access", request.Attachment.ResourceId.SanitizeForLogging());
            return AuthorizationErrors.NoAccessToResource;
        }
        var resourceType = await resourceRegistryService.GetResourceType(request.Attachment.ResourceId, cancellationToken);
        if (resourceType is null)
        {
            logger.LogError("Resource type not found for {ResourceId} despite successful authorization", request.Attachment.ResourceId);
            throw new Exception($"Resource type not found for {request.Attachment.ResourceId}. This should be impossible as authorization worked.");
        }
        if (resourceType != "GenericAccessResource" && resourceType != "CorrespondenceService")
        {
            logger.LogWarning("Incorrect resource type {ResourceType} for {ResourceId}", resourceType, request.Attachment.ResourceId);
            return AuthorizationErrors.IncorrectResourceType;
        }

        var party = await altinnRegisterService.LookUpPartyById(user.GetCallerOrganizationId(), cancellationToken);
        if (party?.PartyUuid is not Guid partyUuid)
        {
            logger.LogError("Could not find party UUID for organization {OrganizationId}", user.GetCallerOrganizationId());
            return AuthorizationErrors.CouldNotFindPartyUuid;
        }
        logger.LogInformation("Retrieved party UUID {PartyUuid} for organization {OrganizationId}", partyUuid, user.GetCallerOrganizationId());
        var attachment = request.Attachment;
        var attachmentNameError = attachmentHelper.ValidateAttachmentName(request.Attachment);
        if (attachmentNameError is not null)
        {
            logger.LogWarning("Invalid attachment name for resource {ResourceId}: {Error}", request.Attachment.ResourceId, attachmentNameError);
            return attachmentNameError;
        }
        if (attachment.Sender.StartsWith("0192:"))
        {
            logger.LogInformation("Converting legacy sender prefix '0192:' to {UrnPrefix} for resource {ResourceId}", 
                UrnConstants.OrganizationNumberAttribute, 
                request.Attachment.ResourceId);
            attachment.Sender = $"{UrnConstants.OrganizationNumberAttribute}:{attachment.Sender.WithoutPrefix()}";
        }
        return await TransactionWithRetriesPolicy.Execute<Guid>(async (cancellationToken) =>
        {
            var initializedAttachment = await attachmentRepository.InitializeAttachment(attachment, cancellationToken);   
            await attachmentHelper.SetAttachmentStatus(initializedAttachment.Id, AttachmentStatus.Initialized, partyUuid, cancellationToken);
            backgroundJobClient.Enqueue<IEventBus>((eventBus) => eventBus.Publish(
                AltinnEventType.AttachmentInitialized, 
                initializedAttachment.ResourceId, 
                initializedAttachment.Id.ToString(), 
                "attachment", 
                initializedAttachment.Sender, 
                CancellationToken.None));
            logger.LogInformation("Successfully initialized attachment {AttachmentId} for resource {ResourceId}", 
                initializedAttachment.Id, 
                request.Attachment.ResourceId);
            return initializedAttachment.Id;
        }, logger, cancellationToken);
    }

}