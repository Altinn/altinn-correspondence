using Altinn.Correspondence.Application.CorrespondenceDueDate;
using Altinn.Correspondence.Application.CreateNotification;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.PublishCorrespondence;
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
    IOptions<GeneralSettings> generalSettings,
    ILogger<InitializeCorrespondencesHandler> logger) : IHandler<InitializeCorrespondencesRequest, InitializeCorrespondencesResponse>
{
    private readonly GeneralSettings _generalSettings = generalSettings.Value;

    public async Task<OneOf<InitializeCorrespondencesResponse, Error>> Process(InitializeCorrespondencesRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(generalSettings.Value.ResourceWhitelist))
        {
            if (!generalSettings.Value.ResourceWhitelist.Split(',').Contains(request.Correspondence.ResourceId))
            {
                return AuthorizationErrors.ResourceNotWhitelisted;
            }
        }
        var hasAccess = await altinnAuthorizationService.CheckAccessAsSender(
        user,
        request.Correspondence.ResourceId,
        request.Correspondence.Sender.WithoutPrefix(),
        null,
        cancellationToken);
        if (!hasAccess)
        {
            return AuthorizationErrors.NoAccessToResource;
        }
        var resourceType = await resourceRegistryService.GetResourceType(request.Correspondence.ResourceId, cancellationToken);
        if (resourceType is null)
        {
            throw new Exception($"Resource type not found for {request.Correspondence.ResourceId}. This should be impossible as authorization worked.");
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
        if (request.Recipients.Count != request.Recipients.Distinct().Count())
        {
            return CorrespondenceErrors.DuplicateRecipients;
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
        var dateError = initializeCorrespondenceHelper.ValidateDateConstraints(request.Correspondence);
        if (dateError != null)
        {
            return dateError;
        }
        var contentError = initializeCorrespondenceHelper.ValidateCorrespondenceContent(request.Correspondence.Content);
        if (contentError != null)
        {
            return contentError;
        }
        var existingAttachmentIds = request.ExistingAttachments;
        var uploadAttachments = request.Attachments;
        var uploadAttachmentMetadata = request.Correspondence.Content.Attachments;

        // Validate that existing attachments are correct
        var getExistingAttachments = await initializeCorrespondenceHelper.GetExistingAttachments(existingAttachmentIds, request.Correspondence.Sender);
        if (getExistingAttachments.IsT1) return getExistingAttachments.AsT1;
        var existingAttachments = getExistingAttachments.AsT0;
        if (existingAttachments.Count != existingAttachmentIds.Count)
        {
            return CorrespondenceErrors.ExistingAttachmentNotFound;
        }
        // Validate that existing attachments are published
        var anyExistingAttachmentsNotPublished = existingAttachments.Any(a => a.GetLatestStatus()?.Status != AttachmentStatus.Published);
        if (anyExistingAttachmentsNotPublished)
        {
            return CorrespondenceErrors.AttachmentsNotPublished;
        }
        // Validate the uploaded files
        var attachmentMetaDataError = initializeCorrespondenceHelper.ValidateAttachmentFiles(uploadAttachments, uploadAttachmentMetadata);
        if (attachmentMetaDataError != null)
        {
            return attachmentMetaDataError;
        }

        // Gather attachments for the correspondence
        var attachmentsToBeUploaded = new List<AttachmentEntity>();
        if (uploadAttachmentMetadata.Count > 0)
        {
            foreach (var attachment in uploadAttachmentMetadata)
            {
                var processedAttachment = await initializeCorrespondenceHelper.ProcessNewAttachment(attachment, partyUuid, cancellationToken);
                attachmentsToBeUploaded.Add(processedAttachment);
            }
        }
        if (existingAttachmentIds.Count > 0)
        {
            attachmentsToBeUploaded.AddRange(existingAttachments.Where(a => a != null).Select(a => a!));
        }

        // Validate notification content if notification is provided
        if (request.Notification != null)
        {
            var templates = await notificationTemplateRepository.GetNotificationTemplates(request.Notification.NotificationTemplate, cancellationToken, request.Correspondence.Content?.Language);
            if (templates.Count == 0)
            {
                return NotificationErrors.TemplateNotFound;
            }
            var notificationError = initializeCorrespondenceHelper.ValidateNotification(request.Notification, request.Recipients);
            if (notificationError != null)
            {
                return notificationError;
            }
        }
        // Upload attachments
        var uploadError = await initializeCorrespondenceHelper.UploadAttachments(attachmentsToBeUploaded, uploadAttachments, partyUuid, cancellationToken);
        if (uploadError != null)
        {
            return uploadError;
        }


        return await TransactionWithRetriesPolicy.Execute(async (cancellationToken) =>
        {
            return await InitializeCorrespondences(request, attachmentsToBeUploaded, partyUuid, reservedRecipients, cancellationToken);
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
                logger.LogInformation("Recipient reserved from correspondences in KRR");
                return CorrespondenceErrors.RecipientReserved(request.Recipients.First());
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
            throw;
        }
    }
    private async Task<OneOf<InitializeCorrespondencesResponse, Error>> InitializeCorrespondences(InitializeCorrespondencesRequest request, List<AttachmentEntity> attachmentsToBeUploaded, Guid partyUuid, List<string> reservedRecipients, CancellationToken cancellationToken)
    {
        logger.LogInformation("Initializing {correspondenceCount} correspondences for {resourceId}", request.Recipients.Count, request.Correspondence.ResourceId);
        var correspondences = new List<CorrespondenceEntity>();
        var recipientsToSearch = request.Recipients.Select(r => r.WithoutPrefix()).ToList();
        var recipientDetails = new List<Party>();
        if (request.Correspondence.Content!.MessageBody.Contains("{{recipientName}}") || request.Correspondence.Content!.MessageTitle.Contains("{{recipientName}}") || request.Correspondence.Content!.MessageSummary.Contains("{{recipientName}}"))
        {
            recipientDetails = await altinnRegisterService.LookUpPartiesByIds(recipientsToSearch, cancellationToken);
            if (recipientDetails == null || recipientDetails?.Count != recipientsToSearch.Count)
            {
                return CorrespondenceErrors.RecipientLookupFailed(recipientsToSearch.Except(recipientDetails != null ? recipientDetails.Select(r => r.SSN ?? r.OrgNumber) : new List<string>()).ToList());
            }
            foreach (var details in recipientDetails)
            {
                if (details.PartyUuid == Guid.Empty)
                {
                    return CorrespondenceErrors.RecipientLookupFailed(new List<string> { details.SSN ?? details.OrgNumber });
                }
            }
        }

        foreach (var recipient in request.Recipients)
        {
            var isReserved = reservedRecipients.Contains(recipient.WithoutPrefix());
            var recipientParty = recipientDetails.FirstOrDefault(r => r.SSN == recipient.WithoutPrefix() || r.OrgNumber == recipient.WithoutPrefix());
            var correspondence = initializeCorrespondenceHelper.MapToCorrespondenceEntity(request, recipient, attachmentsToBeUploaded, partyUuid, recipientParty, isReserved);
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
            if (request.Correspondence.Content.Attachments.Count == 0)
            {
                await hangfireScheduleHelper.SchedulePublishAfterDialogCreated(correspondence.Id, cancellationToken);
            }

            var isReserved = correspondence.GetHighestStatus()?.Status == CorrespondenceStatus.Reserved;
            var notificationDetails = new List<InitializedCorrespondencesNotifications>();
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
                    var notificationJob = backgroundJobClient.Enqueue<CreateNotificationHandler>((handler) => handler.Process(new CreateNotificationRequest 
                    { 
                        NotificationRequest = request.Notification, 
                        CorrespondenceId = correspondence.Id,
                        CorrespondenceContent = correspondence.Content,
                        RequestCorrespondence = correspondence
                    }, cancellationToken));
                }
            }
            if (request.Correspondence.Content.Attachments.Count > 0 && await correspondenceRepository.AreAllAttachmentsPublished(correspondence.Id, cancellationToken))
            {
                await hangfireScheduleHelper.SchedulePublishAfterDialogCreated(correspondence.Id, cancellationToken);
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
        var dialogId = await dialogportenService.CreateCorrespondenceDialog(correspondenceId);
        await correspondenceRepository.AddExternalReference(correspondenceId, ReferenceType.DialogportenDialogId, dialogId);
    }

    public void SchedulePublish(Guid correspondenceId, DateTimeOffset publishTime, CancellationToken cancellationToken)
    {
        backgroundJobClient.Schedule<PublishCorrespondenceHandler>((handler) => handler.Process(correspondenceId, null, cancellationToken), publishTime);
    }
}
