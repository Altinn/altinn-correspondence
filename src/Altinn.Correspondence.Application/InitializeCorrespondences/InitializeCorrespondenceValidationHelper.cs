using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Exceptions;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
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
            if (resourceType != "CorrespondenceService")
            {
                logger.LogError("Incorrect resource type {ResourceType} for {ResourceId}", resourceType, request.Correspondence.ResourceId);
                return AuthorizationErrors.IncorrectResourceType;
            }

            var caller = user?.GetCallerPartyUrn();
            var party = await altinnRegisterService.LookUpPartyById(caller, cancellationToken);
            if (party?.PartyUuid is not Guid partyUuid)
            {
                logger.LogError("Could not find party UUID for caller {caller}", caller);
                return AuthorizationErrors.CouldNotFindPartyUuid;
            }
            validatedData.PartyUuid = partyUuid;

            var confidentialLevel = await resourceRegistryService.GetConfidentialType(request.Correspondence.ResourceId, cancellationToken);
            if (confidentialLevel == ConfidentialTypeEnum.Confidential && !request.Correspondence.IsConfidential)
            {
                logger.LogWarning("Confidential correspondence cannot be initialized without setting the 'IsConfidential' flag to true");
                return CorrespondenceErrors.CannotInitializeConfidentialCorrespondenceWithoutIsConfidentialFlag;
            }
            if (request.Correspondence.IsConfidential && confidentialLevel == ConfidentialTypeEnum.NotConfidential)
            {
                logger.LogWarning("Correspondence cannot be initialized with 'IsConfidential' flag set to true because the resource is not confidential");
                return CorrespondenceErrors.CannotInitializeNonConfidentialCorrespondenceWithIsConfidentialFlag;
            }
            var recipientValidation = await ValidateRecipientParty(request, cancellationToken);
            if (recipientValidation.IsT1)
            {
                return recipientValidation.AsT1;
            }

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

            logger.LogDebug("Validating correspondence sender");
            var senderError = initializeCorrespondenceHelper.ValidateCorrespondenceSender(request.Correspondence);
            if (senderError != null)
            {
                logger.LogWarning("Sender validation failed: {Error}", senderError);
                return senderError;
            }

            logger.LogDebug("Validating external references for Dialogporten transmission type");
            var externalReferences = request.Correspondence.ExternalReferences ?? new List<ExternalReferenceEntity>();
            var externalReferencesError = initializeCorrespondenceHelper.ValidateExternalReferences(externalReferences);
            if (externalReferencesError != null)
            {
                logger.LogWarning("External references validation failed: {Error}", externalReferencesError);
                return externalReferencesError;
            }

            var dialogId = request.Correspondence.ExternalReferences?.FirstOrDefault(er => er.ReferenceType == ReferenceType.DialogportenDialogId)?.ReferenceValue;
            if (!string.IsNullOrWhiteSpace(dialogId))
            {
                if (!Guid.TryParse(dialogId, out _))
                {
                    logger.LogWarning("Provided DialogId {DialogId} is not a valid GUID", dialogId);
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
                logger.LogWarning("Not all existing attachments were found");
                return CorrespondenceErrors.ExistingAttachmentNotFound;
            }

            var totalRequestedAttachments = (existingAttachmentIds?.Count ?? 0) + (uploadAttachmentMetadata?.Count ?? 0);
            if (totalRequestedAttachments > 100)
            {
                logger.LogWarning("Attachment count exceeded: existing={ExistingCount}, new={NewCount}", existingAttachmentIds?.Count ?? 0, uploadAttachmentMetadata?.Count ?? 0);
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
            var uploadError = await initializeCorrespondenceHelper.UploadAttachments(validatedData.AttachmentsToBeUploaded, uploadAttachmentFiles, partyUuid, cancellationToken);
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

        private async Task<OneOf<bool, Error>> ValidateRecipientParty(InitializeCorrespondencesRequest request, CancellationToken cancellationToken)
        {
            var recipientsNotFound = new List<string>();
            var recipientsWithoutRequiredRoles = new List<string>();

            foreach (var recipient in request.Recipients)
            {
                var recipientParty = await altinnRegisterService.LookUpPartyById(recipient, cancellationToken);
                if (recipientParty is null || recipientParty.PartyUuid is null)
                {
                    recipientsNotFound.Add(recipient);
                    continue;
                }

                if (string.IsNullOrEmpty(recipientParty.OrgNumber)) continue;
                var hasRequired = await altinnRegisterService.HasPartyRequiredRoles(recipient, recipientParty.PartyUuid.Value, request.Correspondence.IsConfidential, cancellationToken);
                if (!hasRequired)
                {
                    recipientsWithoutRequiredRoles.Add(recipient);
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
