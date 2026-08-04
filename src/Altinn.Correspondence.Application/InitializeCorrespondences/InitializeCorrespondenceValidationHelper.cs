using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Exceptions;
using Altinn.Correspondence.Core.Extensions;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Register.Contracts;
using Hangfire;
using Microsoft.Extensions.Logging;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.InitializeCorrespondences
{
    public class InitializeCorrespondenceValidationHelper(
    InitializeCorrespondenceHelper initializeCorrespondenceHelper,
    AttachmentHelper attachmentHelper,
    IAltinnAuthorizationService altinnAuthorizationService,
    IAltinnRegisterService altinnRegisterService,
    INotificationTemplateRepository notificationTemplateRepository,
    IResourceRegistryService resourceRegistryService,
    IBackgroundJobClient backgroundJobClient,
    IDialogportenService dialogportenService,
    IContactReservationRegistryService contactReservationRegistryService,
    ILogger<InitializeCorrespondenceValidationHelper> logger)
    {
        internal class ValidatedData
        {
            public List<AttachmentEntity> AttachmentsToBeUploaded { get; set; } = new();
            public List<AttachmentEntity> UploadTargetAttachments { get; set; } = new();
            public Guid PartyUuid { get; set; }
            public List<string> ReservedRecipients { get; set; } = new();
            public List<Party> RecipientDetails { get; set; } = new();
            public string ServiceOwnerOrgNumber { get; set; } = string.Empty;
        }

        internal async Task<OneOf<ValidatedData, Error>> ValidatePrepareDataAndUploadAttachments(
            InitializeCorrespondencesRequest request,
            ClaimsPrincipal? user,
            CancellationToken cancellationToken)
        {
            var validatedData = new ValidatedData();

            var serviceOwnerOrgNumber = await resourceRegistryService.GetServiceOwnerOrganizationNumber(request.Correspondence.ResourceId, cancellationToken) ?? string.Empty;
            if (serviceOwnerOrgNumber is null || serviceOwnerOrgNumber == string.Empty)
            {
                logger.LogError("Service owner/sender's organization number (9 digits) not found for resource {ResourceId}", request.Correspondence.ResourceId);
                return CorrespondenceErrors.InvalidResource;
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
            var isTransmissionCorrespondence = request.Correspondence.ExternalReferences?
                .Any(er => er.ReferenceType == ReferenceType.DialogportenDialogId) == true;
            var resourceTypeAllowed = resourceType == "CorrespondenceService"
                || (resourceType == "AltinnApp" && isTransmissionCorrespondence);
            if (!resourceTypeAllowed)
            {
                logger.LogError(
                    "Incorrect resource type {ResourceType} for {ResourceId}. Resource must be of type CorrespondenceService or AltinnApp (AltinnApp allowed for Dialogporten transmissions)",
                    resourceType,
                    request.Correspondence.ResourceId);
                return AuthorizationErrors.IncorrectCorrespondenceResourceType;
            }

            var caller = user?.GetCallerPartyUrn();
            if (caller is null)
            {
                logger.LogWarning("Could not determine caller for correspondence {ResourceId}", request.Correspondence.ResourceId);
                return AuthorizationErrors.CouldNotDetermineCaller;
            }
            var party = await altinnRegisterService.LookUpPartyById(caller, cancellationToken);
            if (party?.Uuid is not Guid partyUuid)
            {
                logger.LogError("Could not find party UUID for caller {caller}", caller);
                return AuthorizationErrors.CouldNotFindPartyUuid;
            }
            validatedData.PartyUuid = partyUuid;

            if (resourceType != "AltinnApp") 
            { 
                var confidentialLevel = await resourceRegistryService.GetConfidentialType(request.Correspondence.ResourceId, cancellationToken);
                if (confidentialLevel == ConfidentialTypeEnum.Confidential && !request.Correspondence.IsConfidential)
                {
                    return CorrespondenceErrors.CannotInitializeConfidentialCorrespondenceWithoutIsConfidentialFlag;
                }
                if (request.Correspondence.IsConfidential && confidentialLevel == ConfidentialTypeEnum.NotConfidential)
                {
                    return CorrespondenceErrors.CannotInitializeNonConfidentialCorrespondenceWithIsConfidentialFlag;
                }
            }
            if (request.Recipients.Count != request.Recipients.Distinct().Count())
            {
                return CorrespondenceErrors.DuplicateRecipients;
            }

            var recipientValidation = await ValidateRecipientParty(request, cancellationToken);
            if (recipientValidation.IsT1)
            {
                return recipientValidation.AsT1;
            }

            if (request.Correspondence.IsConfirmationNeeded && request.Correspondence.DueDateTime is null)
            {
                return CorrespondenceErrors.DueDateRequired;
            }

            var contactReservation = await HandleContactReservation(request);
            if (contactReservation.TryPickT1(out var error, out var reservedRecipients))
            {
                return error;
            }
            validatedData.ReservedRecipients = reservedRecipients;

            logger.LogDebug("Validating date constraints");
            var dateError = initializeCorrespondenceHelper.ValidateDateConstraints(request.Correspondence);
            if (dateError != null)
            {
                return dateError;
            }

            logger.LogDebug("Validating correspondence content");
            var contentError = initializeCorrespondenceHelper.ValidateCorrespondenceContent(request.Correspondence.Content);
            if (contentError != null)
            {
                return contentError;
            }

            logger.LogDebug("Validating correspondence sender");
            var senderError = initializeCorrespondenceHelper.ValidateCorrespondenceSender(request.Correspondence);
            if (senderError != null)
            {
                return senderError;
            }

            logger.LogDebug("Validating external references for Dialogporten transmission type");
            var externalReferences = request.Correspondence.ExternalReferences ?? new List<ExternalReferenceEntity>();
            var externalReferencesError = initializeCorrespondenceHelper.ValidateExternalReferences(externalReferences);
            if (externalReferencesError != null)
            {
                return externalReferencesError;
            }

            var dialogId = request.Correspondence.ExternalReferences?.FirstOrDefault(er => er.ReferenceType == ReferenceType.DialogportenDialogId)?.ReferenceValue;
            if (!string.IsNullOrWhiteSpace(dialogId))
            {
                if (!Guid.TryParse(dialogId, out _))
                {
                    return CorrespondenceErrors.InvalidCorrespondenceDialogId;
                }
                var validationResult = await ValidateTransmissionRequest(request.Correspondence, request, cancellationToken);
                if (validationResult.IsT1)
                {
                    return validationResult.AsT1;
                }
            }

            var existingAttachmentIds = request.ExistingAttachments;
            var uploadAttachmentFiles = request.Attachments;
            var uploadAttachmentMetadata = request.Correspondence.Content.Attachments;

            logger.LogDebug("Validating {ExistingCount} existing attachments", existingAttachmentIds.Count);
            var getExistingAttachments = await initializeCorrespondenceHelper.GetExistingAttachments(existingAttachmentIds, validatedData.ServiceOwnerOrgNumber);
            if (getExistingAttachments.IsT1) return getExistingAttachments.AsT1;
            var existingAttachments = getExistingAttachments.AsT0;
            if (existingAttachments.Count != existingAttachmentIds.Count)
            {
                return CorrespondenceErrors.ExistingAttachmentNotFound;
            }

            var totalRequestedAttachments = existingAttachmentIds.Count + uploadAttachmentMetadata.Count;
            if (totalRequestedAttachments > 100)
            {
                logger.LogWarning("Attachment count exceeded: existing={ExistingCount}, new={NewCount}", existingAttachmentIds.Count, uploadAttachmentMetadata.Count);
                return CorrespondenceErrors.AttachmentCountExceeded;
            }

            logger.LogDebug("Checking publication status of existing attachments");
            var anyExistingAttachmentsNotPublished = existingAttachments.Any(a => a.GetLatestStatus()?.Status != AttachmentStatus.Published);
            if (anyExistingAttachmentsNotPublished)
            {
                logger.LogWarning("Some existing attachments are not published");
                return CorrespondenceErrors.AttachmentsNotPublished;
            }

            logger.LogDebug("Validating {UploadCount} new attachments", uploadAttachmentFiles.Count);
            var attachmentMetaDataError = initializeCorrespondenceHelper.ValidateAttachmentFiles(uploadAttachmentFiles, uploadAttachmentMetadata);
            if (attachmentMetaDataError != null)
            {
                logger.LogWarning("Attachment validation failed: {Error}", attachmentMetaDataError);
                return attachmentMetaDataError;
            }

            logger.LogDebug("Validating expiration times for attachments");
            var uploadAttachments = uploadAttachmentMetadata.Select(a => a.Attachment).Where(a => a != null).Select(a => a!).ToList();
            var uploadAttachmentsExpirationError = attachmentHelper.ValidateAttachmentsExpiration(uploadAttachments);
            if (uploadAttachmentsExpirationError != null)
            {
                logger.LogWarning("Expiration time validation failed for uploaded attachments: {Error}", uploadAttachmentsExpirationError);
                return uploadAttachmentsExpirationError;
            }

            var existingAttachmentsExpirationError = await initializeCorrespondenceHelper.ValidateAttachmentsExpiration(existingAttachments, cancellationToken);
            if (existingAttachmentsExpirationError != null)
            {
                logger.LogWarning("Expiration time validation failed for existing attachments: {Error}", existingAttachmentsExpirationError);
                return existingAttachmentsExpirationError;
            }

            logger.LogDebug("Validating reply options");
            var replyOptionsError = initializeCorrespondenceHelper.ValidateReplyOptions(request.Correspondence.ReplyOptions);
            if (replyOptionsError != null)
            {
                logger.LogWarning("Reply options validation failed: {Error}", replyOptionsError);
                return replyOptionsError;
            }

            var hasRecipientNamePlaceholder =
                request.Correspondence.Content.MessageBody.Contains("{{recipientName}}", StringComparison.Ordinal) ||
                request.Correspondence.Content.MessageTitle.Contains("{{recipientName}}", StringComparison.Ordinal) ||
                request.Correspondence.Content.MessageSummary.Contains("{{recipientName}}", StringComparison.Ordinal);

            if (hasRecipientNamePlaceholder)
            {
                var recipientsToSearch = request.Recipients.Select(r => r.WithoutPrefix()).ToList();
                validatedData.RecipientDetails = await altinnRegisterService.LookUpPartiesByIds(recipientsToSearch, cancellationToken) ?? [];
                if (validatedData.RecipientDetails.Count != recipientsToSearch.Count)
                {
                    return CorrespondenceErrors.RecipientLookupFailed(recipientsToSearch.Except(
                        validatedData.RecipientDetails.Select(r => r.GetExternalUrn()?.WithoutPrefix() ?? string.Empty)).ToList());
                }
                foreach (var details in validatedData.RecipientDetails)
                {
                    if (details.Uuid == Guid.Empty)
                    {
                        return CorrespondenceErrors.RecipientLookupFailed(new List<string> { details.GetExternalUrn()?.WithoutPrefix() ?? string.Empty });
                    }
                }
            }

            if (request.Notification != null)
            {
                logger.LogDebug("Validating notification template {TemplateId}", request.Notification.NotificationTemplate);
                var templates = await notificationTemplateRepository.GetNotificationTemplates(request.Notification.NotificationTemplate, cancellationToken, request.Correspondence.Content.Language);
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

            logger.LogDebug("Processing attachments for correspondence");
            if (uploadAttachmentMetadata.Count > 0)
            {
                foreach (var attachment in uploadAttachmentMetadata)
                {
                    logger.LogDebug("Processing new attachment {AttachmentId}", attachment.AttachmentId);
                    var processedAttachment = await initializeCorrespondenceHelper.ProcessNewAttachment(attachment, partyUuid, validatedData.ServiceOwnerOrgNumber, cancellationToken);
                    validatedData.AttachmentsToBeUploaded.Add(processedAttachment);
                    if (InitializeCorrespondenceHelper.IsUploadTarget(processedAttachment))
                    {
                        validatedData.UploadTargetAttachments.Add(processedAttachment);
                    }
                }
            }
            if (existingAttachmentIds.Count > 0)
            {
                logger.LogDebug("Adding {Count} existing attachments", existingAttachmentIds.Count);
                validatedData.AttachmentsToBeUploaded.AddRange(existingAttachments);
            }

            logger.LogDebug("Uploading {Count} attachments", validatedData.UploadTargetAttachments.Count);
            var uploadError = await initializeCorrespondenceHelper.UploadAttachments(validatedData.UploadTargetAttachments, uploadAttachmentFiles, partyUuid, cancellationToken);
            if (uploadError != null)
            {
                logger.LogError("Attachment upload failed: {Error}", uploadError);
                return uploadError;
            }

            logger.LogInformation("Validation and data preparation completed successfully");
            return validatedData;
        }

        private async Task<OneOf<bool, Error>> ValidateRecipientParty(InitializeCorrespondencesRequest request, CancellationToken cancellationToken)
        {
            var recipientsNotFound = new List<string>();
            var recipientsWithoutRequiredRoles = new List<string>();

            foreach (var recipient in request.Recipients)
            {
                var recipientParty = await altinnRegisterService.LookUpPartyById(recipient, cancellationToken);
                if (recipientParty is null || recipientParty.Uuid == Guid.Empty)
                {
                    recipientsNotFound.Add(recipient);
                    continue;
                }

                if (string.IsNullOrEmpty(recipientParty.GetOrganizationIdentifier())) continue;
                if (request.Correspondence.IsConfidential)
                {
                    var hasRequired = await altinnRegisterService.HasPartyRequiredRolesForConfidential(recipient, recipientParty.Uuid, cancellationToken);
                    if (!hasRequired)
                    {
                        recipientsWithoutRequiredRoles.Add(recipient);
                    }
                }
            }

            if (recipientsNotFound.Count > 0)
            {
                return CorrespondenceErrors.RecipientLookupFailed(recipientsNotFound);
            }
            if (recipientsWithoutRequiredRoles.Count > 0)
            {
                if (request.Correspondence.IsConfidential)
                {
                    return CorrespondenceErrors.RecipientLacksRequiredRolesForCorrespondence(recipientsWithoutRequiredRoles);
                }
                else
                {
                    var recipients = string.Join(',', recipientsWithoutRequiredRoles);
                    logger.LogWarning($"Role check failed for {recipients}");
                    backgroundJobClient.Enqueue<SlackNotificationService>(slackNotificationService =>
                        slackNotificationService.SendSlackMessage(
                            $"Correspondence recipients {recipients} did not have required roles, " +
                            $"but check was bypassed pending Altinn Register change. See #1444 for details."));
                }
            }

            return true;
        }
        internal async Task<OneOf<Task, Error>> ValidateTransmissionRequest(CorrespondenceEntity correspondence, InitializeCorrespondencesRequest request, CancellationToken cancellationToken)
        {
            if (request.Recipients.Count > 1)
            {
                return CorrespondenceErrors.TransmissionOnlyAllowsOneRecipient;
            }

            if (correspondence.ReplyOptions.Count > 0 || correspondence.IsConfirmationNeeded)
            {
                return CorrespondenceErrors.TransmissionNotAllowedWithGuiActions;
            }

            var dialogId = correspondence.ExternalReferences
                .FirstOrDefault(er => er.ReferenceType == ReferenceType.DialogportenDialogId)?.ReferenceValue;

            if (string.IsNullOrEmpty(dialogId))
            {
                throw new InvalidOperationException("Dialog ID not found on correspondence");
            }
            if (!Guid.TryParse(dialogId, out _))
            {
                return CorrespondenceErrors.InvalidCorrespondenceDialogId;
            }
            try
            {
                var validateResourceOwnerMatch = await dialogportenService.DialogValidForTransmission(dialogId, correspondence.ResourceId, cancellationToken);
                if (validateResourceOwnerMatch == false)
                {
                    return CorrespondenceErrors.InvalidServiceOwner;
                }
                var expectedRecipient = request.Recipients.First();
                var recipientMatches = await dialogportenService.ValidateDialogRecipientMatch(dialogId, expectedRecipient, cancellationToken);
                if (recipientMatches == false)
                {
                    return CorrespondenceErrors.RecipientMismatch;
                }
            }
            catch (DialogNotFoundException)
            {
                return CorrespondenceErrors.DialogportenDialogIdNotFound;
            }
            {
                return Task.CompletedTask;
            }
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
    }
}
