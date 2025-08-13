using Altinn.Correspondence.Application.CorrespondenceDueDate;
using Altinn.Correspondence.Application.CreateNotification;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Caching;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Hangfire;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneOf;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace Altinn.Correspondence.Application.InitializeCorrespondences;

public class InitializeCorrespondencesHandler(
    InitializeCorrespondenceHelper initializeCorrespondenceHelper,
    IAltinnAuthorizationService altinnAuthorizationService,
    IAltinnRegisterService altinnRegisterService,
    ICorrespondenceRepository correspondenceRepository,
    INotificationTemplateRepository notificationTemplateRepository,
    IResourceRegistryService resourceRegistryService,
    IBackgroundJobClient backgroundJobClient,
    IDialogportenService dialogportenService,
    IContactReservationRegistryService contactReservationRegistryService,
    IHybridCacheWrapper hybridCacheWrapper,
    HangfireScheduleHelper hangfireScheduleHelper,
    IIdempotencyKeyRepository idempotencyKeyRepository,
    IOptions<GeneralSettings> generalSettings,
    ILogger<InitializeCorrespondencesHandler> logger) : IHandler<InitializeCorrespondencesRequest, InitializeCorrespondencesResponse>
{
    private class ValidatedData
    {
        public List<AttachmentEntity> AttachmentsToBeUploaded { get; set; } = new();
        public Guid PartyUuid { get; set; }
        public List<string> ReservedRecipients { get; set; } = new();
        public List<Party> RecipientDetails { get; set; } = new();
        public string ServiceOwnerOrgNumber { get; set; } = string.Empty;
    }

    private async Task<OneOf<ValidatedData, Error>> ValidatePrepareDataAndUploadAttachments(
        InitializeCorrespondencesRequest request,
        ClaimsPrincipal? user,
        CancellationToken cancellationToken)
    {
        var validatedData = new ValidatedData();

        if (!string.IsNullOrWhiteSpace(generalSettings.Value.ResourceWhitelist))
        {
            if (!generalSettings.Value.ResourceWhitelist.Split(',').Contains(request.Correspondence.ResourceId))
            {
                logger.LogError("Resource {ResourceId} is not whitelisted", request.Correspondence.ResourceId);
                return AuthorizationErrors.ResourceNotWhitelisted;
            }
        }

        var serviceOwnerOrgNumber = await resourceRegistryService.GetServiceOwnerOrganizationNumber(request.Correspondence.ResourceId, cancellationToken) ?? string.Empty;
        if (serviceOwnerOrgNumber is null || serviceOwnerOrgNumber == string.Empty)
        {
            logger.LogError("Service owner/sender's organization number (9 digits) not found for resource {ResourceId}", request.Correspondence.ResourceId);
            return CorrespondenceErrors.ServiceOwnerOrgNumberNotFound;
        }
        validatedData.ServiceOwnerOrgNumber = serviceOwnerOrgNumber;

        var hasAccess = await altinnAuthorizationService.CheckAccessAsSender(
            user,
            request.Correspondence.ResourceId,
            validatedData.ServiceOwnerOrgNumber.WithoutPrefix(),
            null,
            cancellationToken);
            
        if (!hasAccess)
        {
            logger.LogWarning("Access denied for resource {ResourceId}", request.Correspondence.ResourceId);
            return AuthorizationErrors.NoAccessToResource;
        }

        var resourceType = await resourceRegistryService.GetResourceType(request.Correspondence.ResourceId, cancellationToken);
        if (resourceType is null)
        {
            logger.LogError("Resource type not found for {ResourceId} despite successful authorization", request.Correspondence.ResourceId);
            throw new Exception($"Resource type not found for {request.Correspondence.ResourceId}. This should be impossible as authorization worked.");
        }
        if (resourceType != "GenericAccessResource" && resourceType != "CorrespondenceService")
        {
            logger.LogError("Incorrect resource type {ResourceType} for {ResourceId}", resourceType, request.Correspondence.ResourceId);
            return AuthorizationErrors.IncorrectResourceType;
        }

        var party = await altinnRegisterService.LookUpPartyById(user.GetCallerOrganizationId(), cancellationToken);
        if (party?.PartyUuid is not Guid partyUuid)
        {
            logger.LogError("Could not find party UUID for organization {OrganizationId}", user.GetCallerOrganizationId());
            return AuthorizationErrors.CouldNotFindPartyUuid;
        }
        validatedData.PartyUuid = partyUuid;

        if (request.Recipients.Count != request.Recipients.Distinct().Count())
        {
            logger.LogWarning("Duplicate recipients found in request");
            return CorrespondenceErrors.DuplicateRecipients;
        }

        if (request.Correspondence.IsConfirmationNeeded && request.Correspondence.DueDateTime is null)
        {
            logger.LogWarning("Due date is required for correspondence requiring confirmation");
            return CorrespondenceErrors.DueDateRequired;
        }

        var contactReservation = await HandleContactReservation(request);
        if (contactReservation.TryPickT1(out var error, out var reservedRecipients))
        {
            logger.LogWarning("Contact reservation failed: {Error}", error);
            return error;
        }
        validatedData.ReservedRecipients = reservedRecipients;

        logger.LogDebug("Validating date constraints");
        var dateError = initializeCorrespondenceHelper.ValidateDateConstraints(request.Correspondence);
        if (dateError != null)
        {
            logger.LogWarning("Date validation failed: {Error}", dateError);
            return dateError;
        }

        logger.LogDebug("Validating correspondence content");
        var contentError = initializeCorrespondenceHelper.ValidateCorrespondenceContent(request.Correspondence.Content);
        if (contentError != null)
        {
            logger.LogWarning("Content validation failed: {Error}", contentError);
            return contentError;
        }

        var existingAttachmentIds = request.ExistingAttachments;
        var uploadAttachments = request.Attachments;
        var uploadAttachmentMetadata = request.Correspondence.Content.Attachments;

        logger.LogDebug("Validating {ExistingCount} existing attachments", existingAttachmentIds.Count);
        var getExistingAttachments = await initializeCorrespondenceHelper.GetExistingAttachments(existingAttachmentIds, validatedData.ServiceOwnerOrgNumber);
        if (getExistingAttachments.IsT1) return getExistingAttachments.AsT1;
        var existingAttachments = getExistingAttachments.AsT0;
        if (existingAttachments.Count != existingAttachmentIds.Count)
        {
            logger.LogWarning("Not all existing attachments were found");
            return CorrespondenceErrors.ExistingAttachmentNotFound;
        }

        logger.LogDebug("Checking publication status of existing attachments");
        var anyExistingAttachmentsNotPublished = existingAttachments.Any(a => a.GetLatestStatus()?.Status != AttachmentStatus.Published);
        if (anyExistingAttachmentsNotPublished)
        {
            logger.LogWarning("Some existing attachments are not published");
            return CorrespondenceErrors.AttachmentsNotPublished;
        }

        logger.LogDebug("Validating {UploadCount} new attachments", uploadAttachments.Count);
        var attachmentMetaDataError = initializeCorrespondenceHelper.ValidateAttachmentFiles(uploadAttachments, uploadAttachmentMetadata);
        if (attachmentMetaDataError != null)
        {
            logger.LogWarning("Attachment validation failed: {Error}", attachmentMetaDataError);
            return attachmentMetaDataError;
        }

        logger.LogDebug("Validating reply options");
        var replyOptionsError = initializeCorrespondenceHelper.ValidateReplyOptions(request.Correspondence.ReplyOptions);
        if (replyOptionsError != null)
        {
            logger.LogWarning("Reply options validation failed: {Error}", replyOptionsError);
            return replyOptionsError;
        }

        logger.LogDebug("Processing attachments for correspondence");
        if (uploadAttachmentMetadata.Count > 0)
        {
            foreach (var attachment in uploadAttachmentMetadata)
            {
                logger.LogDebug("Processing new attachment {AttachmentId}", attachment.AttachmentId);
                var processedAttachment = await initializeCorrespondenceHelper.ProcessNewAttachment(attachment, partyUuid, validatedData.ServiceOwnerOrgNumber, cancellationToken);
                validatedData.AttachmentsToBeUploaded.Add(processedAttachment);
            }
        }
        if (existingAttachmentIds.Count > 0)
        {
            logger.LogDebug("Adding {Count} existing attachments", existingAttachmentIds.Count);
            validatedData.AttachmentsToBeUploaded.AddRange(existingAttachments.Where(a => a != null).Select(a => a!));
        }

        if (request.Notification != null)
        {
            logger.LogDebug("Validating notification template {TemplateId}", request.Notification.NotificationTemplate);
            var templates = await notificationTemplateRepository.GetNotificationTemplates(request.Notification.NotificationTemplate, cancellationToken, request.Correspondence.Content?.Language);
            if (templates.Count == 0)
            {
                logger.LogWarning("Notification template {TemplateId} not found", request.Notification.NotificationTemplate);
                return NotificationErrors.TemplateNotFound;
            }
            var notificationError = initializeCorrespondenceHelper.ValidateNotification(request.Notification, request.Recipients);
            if (notificationError != null)
            {
                logger.LogWarning("Notification validation failed with an error.");
                return notificationError;
            }
        }

        logger.LogDebug("Uploading {Count} attachments", validatedData.AttachmentsToBeUploaded.Count);
        var uploadError = await initializeCorrespondenceHelper.UploadAttachments(validatedData.AttachmentsToBeUploaded, uploadAttachments, partyUuid, cancellationToken);
        if (uploadError != null)
        {
            logger.LogError("Attachment upload failed: {Error}", uploadError);
            return uploadError;
        }

        if (request.Correspondence.Content!.MessageBody.Contains("{{recipientName}}") || 
            request.Correspondence.Content!.MessageTitle.Contains("{{recipientName}}") || 
            request.Correspondence.Content!.MessageSummary.Contains("{{recipientName}}"))
        {
            var recipientsToSearch = request.Recipients.Select(r => r.WithoutPrefix()).ToList();
            validatedData.RecipientDetails = await altinnRegisterService.LookUpPartiesByIds(recipientsToSearch, cancellationToken);
            if (validatedData.RecipientDetails == null || validatedData.RecipientDetails.Count != recipientsToSearch.Count)
            {
                return CorrespondenceErrors.RecipientLookupFailed(recipientsToSearch.Except(
                    validatedData.RecipientDetails != null ? 
                    validatedData.RecipientDetails.Select(r => r.SSN ?? r.OrgNumber) : 
                    new List<string>()).ToList());
            }
            foreach (var details in validatedData.RecipientDetails)
            {
                if (details.PartyUuid == Guid.Empty)
                {
                    return CorrespondenceErrors.RecipientLookupFailed(new List<string> { details.SSN ?? details.OrgNumber });
                }
            }
        }

        logger.LogInformation("Validation and data preparation completed successfully");
        return validatedData;
    }

    public async Task<OneOf<InitializeCorrespondencesResponse, Error>> Process(InitializeCorrespondencesRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        logger.LogInformation("Processing correspondence initialization request for resource {ResourceId}", request.Correspondence.ResourceId);

        if (request.IdempotentKey.HasValue)
        {
            logger.LogInformation("Checking idempotency key {Key}", request.IdempotentKey.Value);
            var result = await TransactionWithRetriesPolicy.Execute<OneOf<InitializeCorrespondencesResponse, Error>>(async (cancellationToken) =>
            {
                var existingKey = await idempotencyKeyRepository.GetByIdAsync(request.IdempotentKey.Value, cancellationToken);
                if (existingKey != null)
                {
                    logger.LogWarning("Duplicate idempotency key {Key} found", request.IdempotentKey.Value);
                    return CorrespondenceErrors.DuplicateInitCorrespondenceRequest;
                }

                logger.LogInformation("Creating new idempotency key {Key}", request.IdempotentKey.Value);
                var idempotencyKey = new IdempotencyKeyEntity()
                {
                    Id = request.IdempotentKey.Value,
                    CorrespondenceId = null,
                    AttachmentId = null,
                    StatusAction = null,
                    IdempotencyType = IdempotencyType.Correspondence
                };
                
                try
                {
                    await idempotencyKeyRepository.CreateAsync(idempotencyKey, cancellationToken);
                    return new OneOf<InitializeCorrespondencesResponse, Error>();
                }
                catch (DbUpdateException e)
                {
                    var sqlState = e.InnerException?.Data["SqlState"]?.ToString();
                    if (sqlState == "23505") // PostgreSQL unique constraint violation
                    {
                        logger.LogWarning("Idempotency key {Key} already exists in database", request.IdempotentKey.Value);
                        return CorrespondenceErrors.DuplicateInitCorrespondenceRequest;
                    }
                    throw;
                }
            }, logger, cancellationToken);

            if (result.IsT1)
            {
                return result.AsT1;
            }
        }

        var validationResult = await ValidatePrepareDataAndUploadAttachments(request, user, cancellationToken);
        if (validationResult.IsT1)
        {
            if (request.IdempotentKey.HasValue)
            {
                logger.LogInformation("Deleting idempotency key {Key} due to validation failure", request.IdempotentKey.Value);
                await idempotencyKeyRepository.DeleteAsync(request.IdempotentKey.Value, cancellationToken);
            }
            return validationResult.AsT1;
        }

        var validatedData = validationResult.AsT0;
        logger.LogInformation("Initializing correspondences with validated data");
        return await TransactionWithRetriesPolicy.Execute(async (cancellationToken) =>
        {
            return await InitializeCorrespondences(
                request,
                validatedData,
                cancellationToken);
        }, logger, cancellationToken);
    }

    private async Task<OneOf<List<string>, Error>> HandleContactReservation(InitializeCorrespondencesRequest request)
    {
        var ignoreReservation = request.Correspondence.IgnoreReservation == true;
        try
        {
            var reservedRecipients = await contactReservationRegistryService.GetReservedRecipients(request.Recipients.Where(recipient => recipient.IsSocialSecurityNumber()).ToList());
            if (!ignoreReservation && request.Recipients.Count == 1 && reservedRecipients.Count == 1)
            {
                logger.LogInformation("Recipient {Recipient} is reserved from correspondences in KRR", request.Recipients[0]);
                return CorrespondenceErrors.RecipientReserved(request.Recipients[0]);
            }
            return reservedRecipients;
        }
        catch (Exception e)
        {
            logger.LogError(e, $"Failed to get reserved recipients from KRR: {e.Message}");
            if (ignoreReservation)
            {
                logger.LogWarning(e, "Processing anyway because ignoreReservation flag is set to true");
                return new List<string>();
            }
            return CorrespondenceErrors.ContactReservationRegistryFailed;
        }
    }

    private async Task<OneOf<InitializeCorrespondencesResponse, Error>> InitializeCorrespondences(
        InitializeCorrespondencesRequest request,
        ValidatedData validatedData,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Initializing {correspondenceCount} correspondences for {resourceId}", 
            request.Recipients.Count, 
            request.Correspondence.ResourceId.SanitizeForLogging());
        
        var correspondences = new List<CorrespondenceEntity>();
        var serviceOwnerOrgNumber = validatedData.ServiceOwnerOrgNumber;
        foreach (var recipient in request.Recipients)
        {
            var isReserved = validatedData.ReservedRecipients.Contains(recipient.WithoutPrefix());
            var recipientParty = validatedData.RecipientDetails.FirstOrDefault(r => r.SSN == recipient.WithoutPrefix() || r.OrgNumber == recipient.WithoutPrefix());
            var correspondence = await initializeCorrespondenceHelper.MapToCorrespondenceEntityAsync(request, recipient, validatedData.AttachmentsToBeUploaded, validatedData.PartyUuid, recipientParty, isReserved, serviceOwnerOrgNumber, cancellationToken);
            correspondences.Add(correspondence);
        }
        await correspondenceRepository.CreateCorrespondences(correspondences, cancellationToken);
        var initializedCorrespondences = new List<InitializedCorrespondences>();
        foreach (var correspondence in correspondences)
        {
            logger.LogInformation("Correspondence {correspondenceId} initialized", correspondence.Id);
            var dialogJob = backgroundJobClient.Enqueue(() => CreateDialogportenDialog(correspondence.Id));
            await hybridCacheWrapper.SetAsync($"dialogJobId_{correspondence.Id}", dialogJob, new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromHours(24)
            });
            if (request.Correspondence.Content!.Attachments.Count == 0 || await correspondenceRepository.AreAllAttachmentsPublished(correspondence.Id, cancellationToken))
            {
                await hangfireScheduleHelper.SchedulePublishAfterDialogCreated(correspondence.Id, cancellationToken);
            }

            var isReserved = correspondence.GetHighestStatus()?.Status == CorrespondenceStatus.Reserved;
            if (!isReserved)
            {
                if (correspondence.DueDateTime is not null)
                {
                    backgroundJobClient.Schedule<CorrespondenceDueDateHandler>((handler) => handler.Process(correspondence.Id, cancellationToken), correspondence.DueDateTime.Value);
                }
                backgroundJobClient.Enqueue<IEventBus>((eventBus) => eventBus.Publish(AltinnEventType.CorrespondenceInitialized, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, CancellationToken.None));

                if (request.Notification != null)
                {
                    // Schedule notification creation as a background job
                    backgroundJobClient.Enqueue<CreateNotificationHandler>((handler) => handler.Process(new CreateNotificationRequest
                    {
                        NotificationRequest = request.Notification,
                        CorrespondenceId = correspondence.Id,
                        Language = correspondence.Content != null ? correspondence.Content.Language : null,
                    }, cancellationToken));
                }
            }
            initializedCorrespondences.Add(new InitializedCorrespondences()
            {
                CorrespondenceId = correspondence.Id,
                Status = correspondence.GetHighestStatus().Status,
                Recipient = correspondence.Recipient
            });
        }

        return new InitializeCorrespondencesResponse()
        {
            Correspondences = initializedCorrespondences,
            AttachmentIds = correspondences.SelectMany(c => c.Content?.Attachments.Select(a => a.AttachmentId)).Distinct().ToList()
        };
    }

    public async Task CreateDialogportenDialog(Guid correspondenceId)
    {
        logger.LogInformation("Creating Dialogporten dialog for correspondence {CorrespondenceId}", correspondenceId);
        var dialogId = await dialogportenService.CreateCorrespondenceDialog(correspondenceId);
        await correspondenceRepository.AddExternalReference(correspondenceId, ReferenceType.DialogportenDialogId, dialogId);
        logger.LogInformation("Successfully created Dialogporten dialog for correspondence {CorrespondenceId}", correspondenceId);
    }
}
