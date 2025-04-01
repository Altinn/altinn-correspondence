using Altinn.Correspondence.Application.CorrespondenceDueDate;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.PublishCorrespondence;
using Altinn.Correspondence.Common.Caching;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Hangfire;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneOf;
using System.Security.Claims;
using System.Text.Json;

namespace Altinn.Correspondence.Application.InitializeCorrespondences;

public class InitializeCorrespondencesHandler(
    InitializeCorrespondenceHelper initializeCorrespondenceHelper,
    IAltinnAuthorizationService altinnAuthorizationService,
    IAltinnNotificationService altinnNotificationService,
    IAltinnRegisterService altinnRegisterService,
    ICorrespondenceRepository correspondenceRepository,
    ICorrespondenceNotificationRepository correspondenceNotificationRepository,
    INotificationTemplateRepository notificationTemplateRepository,
    ICorrespondenceStatusRepository correspondenceStatusRepository,
    IResourceRegistryService resourceRegistryService,
    IBackgroundJobClient backgroundJobClient,
    IDialogportenService dialogportenService,
    IContactReservationRegistryService contactReservationRegistryService,
    IHostEnvironment hostEnvironment,
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
        if(contactReservation.TryPickT1(out var error, out var reservedRecipients))
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
        List<NotificationContent>? notificationContents = null;
        if (request.Notification != null)
        {
            var templates = await notificationTemplateRepository.GetNotificationTemplates(request.Notification.NotificationTemplate, cancellationToken, request.Correspondence.Content?.Language);
            if (templates.Count == 0)
            {
                return NotificationErrors.TemplateNotFound;
            }
            notificationContents = await GetNotificationContent(request.Notification, templates, request.Correspondence, cancellationToken, request.Correspondence.Content?.Language);
            if (notificationContents.Count == 0)
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
            return await InitializeCorrespondences(request, attachmentsToBeUploaded, notificationContents, partyUuid, reservedRecipients, cancellationToken);
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
    private async Task<OneOf<InitializeCorrespondencesResponse, Error>> InitializeCorrespondences(InitializeCorrespondencesRequest request, List<AttachmentEntity> attachmentsToBeUploaded, List<NotificationContent>? notificationContents, Guid partyUuid, List<string> reservedRecipients, CancellationToken cancellationToken)
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
            if (correspondence.GetHighestStatus()?.Status == CorrespondenceStatus.Initialized ||
                correspondence.GetHighestStatus()?.Status == CorrespondenceStatus.ReadyForPublish ||
                correspondence.GetHighestStatus()?.Status == CorrespondenceStatus.Published)
            {
                if (request.Correspondence.Content.Attachments.Count == 0) 
                {
                    await hangfireScheduleHelper.PrepareForPublish(correspondence, cancellationToken);
                }
                else
                {
                    // Will be used to ensure publish always occurs after dialog has been successfully created
                    await hybridCacheWrapper.SetAsync("dialogJobId_" + correspondence.Id, dialogJob, new HybridCacheEntryOptions
                    {
                        Expiration = TimeSpan.FromHours(24)
                    }); 
                }
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
                    var notifications = await CreateNotifications(request.Notification, correspondence, notificationContents, cancellationToken);
                    foreach (var notification in notifications)
                    {
                        var notificationOrder = await altinnNotificationService.CreateNotification(notification, cancellationToken);
                        if (notificationOrder is null)
                        {
                            notificationDetails.Add(new InitializedCorrespondencesNotifications()
                            {
                                OrderId = Guid.Empty,
                                Status = InitializedNotificationStatus.Failure
                            });
                            continue;
                        }
                        var entity = new CorrespondenceNotificationEntity()
                        {
                            Created = DateTimeOffset.UtcNow,
                            NotificationChannel = request.Notification.NotificationChannel,
                            NotificationTemplate = request.Notification.NotificationTemplate,
                            CorrespondenceId = correspondence.Id,
                            NotificationOrderId = notificationOrder.OrderId,
                            RequestedSendTime = notification.RequestedSendTime,
                            IsReminder = notification.RequestedSendTime != notifications[0].RequestedSendTime,
                            OrderRequest = JsonSerializer.Serialize(notification)
                        };

                        notificationDetails.Add(new InitializedCorrespondencesNotifications()
                        {
                            OrderId = entity.NotificationOrderId,
                            IsReminder = entity.IsReminder,
                            // For custom recipients, RecipientLookup will be null. As such, this also maps to Success
                            Status = notificationOrder.RecipientLookup?.Status == RecipientLookupStatus.Failed ? InitializedNotificationStatus.MissingContact : InitializedNotificationStatus.Success
                        });
                        await correspondenceNotificationRepository.AddNotification(entity, cancellationToken);
                        backgroundJobClient.ContinueJobWith<IDialogportenService>(dialogJob, (dialogportenService) => dialogportenService.CreateInformationActivity(correspondence.Id, DialogportenActorType.ServiceOwner, DialogportenTextType.NotificationOrderCreated, notification.RequestedSendTime.ToString("yyyy-MM-dd HH:mm")));
                    }
                }
            }
            if (request.Correspondence.Content.Attachments.Count > 0 && await correspondenceRepository.AreAllAttachmentsPublished(correspondence.Id, cancellationToken))
            {
                await correspondenceStatusRepository.AddCorrespondenceStatus(
                    new CorrespondenceStatusEntity
                    {
                        CorrespondenceId = correspondence.Id,
                        Status = CorrespondenceStatus.ReadyForPublish,
                        StatusChanged = DateTime.UtcNow,
                        StatusText = CorrespondenceStatus.ReadyForPublish.ToString(),
                        PartyUuid = partyUuid
                    },
                    cancellationToken
                );
                await hangfireScheduleHelper.PrepareForPublish(correspondence, cancellationToken);
            }
            initializedCorrespondences.Add(new InitializedCorrespondences()
            {
                CorrespondenceId = correspondence.Id,
                Status = correspondence.GetHighestStatus().Status,
                Recipient = correspondence.Recipient,
                Notifications = notificationDetails
            });
        }
        return new InitializeCorrespondencesResponse()
        {
            Correspondences = initializedCorrespondences,
            AttachmentIds = correspondences.SelectMany(c => c.Content?.Attachments.Select(a => a.AttachmentId)).Distinct().ToList()
        };
    }

    private async Task<List<NotificationOrderRequest>> CreateNotifications(NotificationRequest notification, CorrespondenceEntity correspondence, List<NotificationContent> contents, CancellationToken cancellationToken)
    {
        var notifications = new List<NotificationOrderRequest>();
        string recipientWithoutPrefix = correspondence.Recipient.WithoutPrefix();
        bool isOrganization = recipientWithoutPrefix.IsOrganizationNumber();
        bool isPerson = recipientWithoutPrefix.IsSocialSecurityNumber();

        var recipientOverrides = notification.CustomNotificationRecipients ?? [];
        var newRecipients = new List<Recipient>();
        foreach (var recipientOverride in recipientOverrides)
        {
            newRecipients.AddRange(recipientOverride.Recipients.Select(r => new Recipient
            {
                EmailAddress = r.EmailAddress,
                MobileNumber = r.MobileNumber,
                IsReserved = r.IsReserved,
                OrganizationNumber = r.OrganizationNumber,
                NationalIdentityNumber = r.NationalIdentityNumber
            }));
        }

        List<Recipient> relevantRecipients = newRecipients.Count > 0 ? newRecipients : new List<Recipient>
        {
            new()
            {
                OrganizationNumber = isOrganization ? recipientWithoutPrefix : null,
                NationalIdentityNumber = isPerson ? recipientWithoutPrefix : null
            }
        };

        NotificationContent? content = null;
        if (isOrganization)
        {
            content = contents.FirstOrDefault(c => c.RecipientType == RecipientType.Organization) ?? contents.FirstOrDefault(c => c.RecipientType == null);
        }
        else if (isPerson)
        {
            content = contents.FirstOrDefault(c => c.RecipientType == RecipientType.Person) ?? contents.FirstOrDefault(c => c.RecipientType == null);
        }
        await SetRecipientNameOnNotificationContent(content, correspondence.Recipient, cancellationToken);
        var notificationOrder = new NotificationOrderRequest
        {
            IgnoreReservation = correspondence.IgnoreReservation,
            Recipients = relevantRecipients,
            ResourceId = correspondence.ResourceId,
            RequestedSendTime = correspondence.RequestedPublishTime.UtcDateTime <= DateTime.UtcNow ? DateTime.UtcNow.AddMinutes(5) : correspondence.RequestedPublishTime.UtcDateTime.AddMinutes(5),
            SendersReference = correspondence.SendersReference,
            ConditionEndpoint = CreateConditionEndpoint(correspondence.Id.ToString()),
            NotificationChannel = notification.NotificationChannel,
            EmailTemplate = !string.IsNullOrWhiteSpace(content.EmailSubject) && !string.IsNullOrWhiteSpace(content.EmailBody) ? new EmailTemplate
            {
                Subject = content.EmailSubject,
                Body = content.EmailBody,
            } : null,
            SmsTemplate = !string.IsNullOrWhiteSpace(content.SmsBody) ? new SmsTemplate
            {
                Body = content.SmsBody,
            } : null
        };
        notifications.Add(notificationOrder);
        if (notification.SendReminder)
        {
            notifications.Add(new NotificationOrderRequest
            {
                IgnoreReservation = correspondence.IgnoreReservation,
                Recipients = relevantRecipients,
                ResourceId = correspondence.ResourceId,
                RequestedSendTime = hostEnvironment.IsProduction() ? notificationOrder.RequestedSendTime.AddDays(7) : notificationOrder.RequestedSendTime.AddHours(1),
                ConditionEndpoint = CreateConditionEndpoint(correspondence.Id.ToString()),
                SendersReference = correspondence.SendersReference,
                NotificationChannel = notification.ReminderNotificationChannel ?? notification.NotificationChannel,
                EmailTemplate = !string.IsNullOrWhiteSpace(content.ReminderEmailSubject) && !string.IsNullOrWhiteSpace(content.ReminderEmailBody) ? new EmailTemplate
                {
                    Subject = content.ReminderEmailSubject,
                    Body = content.ReminderEmailBody,
                } : null,
                SmsTemplate = !string.IsNullOrWhiteSpace(content.ReminderSmsBody) ? new SmsTemplate
                {
                    Body = content.ReminderSmsBody,
                } : null
            });
        }
        return notifications;
    }
    private async Task SetRecipientNameOnNotificationContent(NotificationContent? content, string recipient, CancellationToken cancellationToken)
    {
        if (content == null)
        {
            return;
        }
        var recipientName = await altinnRegisterService.LookUpName(recipient.WithoutPrefix(), cancellationToken);
        if (string.IsNullOrEmpty(recipientName))
        {
            return;
        }
        content.EmailBody = content.EmailBody?.Replace("$correspondenceRecipientName$", recipientName);
        content.EmailSubject = content.EmailSubject?.Replace("$correspondenceRecipientName$", recipientName);
        content.SmsBody = content.SmsBody?.Replace("$correspondenceRecipientName$", recipientName);
        content.ReminderEmailBody = content.ReminderEmailBody?.Replace("$correspondenceRecipientName$", recipientName);
        content.ReminderEmailSubject = content.ReminderEmailSubject?.Replace("$correspondenceRecipientName$", recipientName);
        content.ReminderSmsBody = content.ReminderSmsBody?.Replace("$correspondenceRecipientName$", recipientName);
    }
    private async Task<List<NotificationContent>> GetNotificationContent(NotificationRequest request, List<NotificationTemplateEntity> templates, CorrespondenceEntity correspondence, CancellationToken cancellationToken, string? language = null)
    {
        var content = new List<NotificationContent>();
        var sendersName = correspondence.MessageSender;
        if (string.IsNullOrEmpty(sendersName))
        {
            sendersName = await altinnRegisterService.LookUpName(correspondence.Sender.WithoutPrefix(), cancellationToken);
        }
        foreach (var template in templates)
        {
            content.Add(new NotificationContent()
            {
                EmailSubject = CreateNotificationContentFromToken(template.EmailSubject, request.EmailSubject).Replace("$sendersName$", sendersName),
                EmailBody = CreateNotificationContentFromToken(template.EmailBody, request.EmailBody).Replace("$sendersName$", sendersName),
                SmsBody = CreateNotificationContentFromToken(template.SmsBody, request.SmsBody).Replace("$sendersName$", sendersName),
                ReminderEmailBody = CreateNotificationContentFromToken(template.ReminderEmailBody, request.ReminderEmailBody).Replace("$sendersName$", sendersName),
                ReminderEmailSubject = CreateNotificationContentFromToken(template.ReminderEmailSubject, request.ReminderEmailSubject).Replace("$sendersName$", sendersName),
                ReminderSmsBody = CreateNotificationContentFromToken(template.ReminderSmsBody, request.ReminderSmsBody).Replace("$sendersName$", sendersName),
                Language = template.Language,
                RecipientType = template.RecipientType
            });
        }
        return content;
    }

    private Uri? CreateConditionEndpoint(string correspondenceId)
    {
        var conditionEndpoint = new Uri($"{_generalSettings.CorrespondenceBaseUrl.TrimEnd('/')}/correspondence/api/v1/correspondence/{correspondenceId}/notification/check");
        if (conditionEndpoint.Host == "localhost")
        {
            return null;
        }
        return conditionEndpoint;
    }

    private string CreateNotificationContentFromToken(string message, string? token = "")
    {
        return message.Replace("{textToken}", token + " ").Trim();
    }

    public async Task CreateDialogportenDialog(Guid correspondenceId)
    {
        var dialogId = await dialogportenService.CreateCorrespondenceDialog(correspondenceId);
        await correspondenceRepository.AddExternalReference(correspondenceId, ReferenceType.DialogportenDialogId, dialogId);
    }

    internal class NotificationContent
    {
        public string? EmailSubject { get; set; }
        public string? EmailBody { get; set; }
        public string? SmsBody { get; set; }
        public string? ReminderEmailBody { get; set; }
        public string? ReminderEmailSubject { get; set; }
        public string? ReminderSmsBody { get; set; }
        public string? Language { get; set; }
        public RecipientType? RecipientType { get; set; }
    }
}
