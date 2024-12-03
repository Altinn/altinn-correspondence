using Altinn.Correspondence.Application.CorrespondenceDueDate;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.PublishCorrespondence;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Hangfire;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneOf;
using ReverseMarkdown.Converters;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.InitializeCorrespondences;

public class InitializeCorrespondencesHandler(
    InitializeCorrespondenceHelper initializeCorrespondenceHelper,
    IAltinnAuthorizationService altinnAuthorizationService,
    IAltinnNotificationService altinnNotificationService,
    IAltinnRegisterService altinnRegisterService,
    ICorrespondenceRepository correspondenceRepository,
    ICorrespondenceNotificationRepository correspondenceNotificationRepository,
    INotificationTemplateRepository notificationTemplateRepository,
    IEventBus eventBus,
    IBackgroundJobClient backgroundJobClient,
    IDialogportenService dialogportenService,
    IHostEnvironment hostEnvironment,
    IOptions<GeneralSettings> generalSettings,
    ILogger<InitializeCorrespondencesHandler> logger) : IHandler<InitializeCorrespondencesRequest, InitializeCorrespondencesResponse>
{
    private readonly GeneralSettings _generalSettings = generalSettings.Value;

    public async Task<OneOf<InitializeCorrespondencesResponse, Error>> Process(InitializeCorrespondencesRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
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
        // Validate that uploaded files match attachment metadata
        var attachmentMetaDataError = InitializeCorrespondenceHelper.ValidateAttachmentFiles(uploadAttachments, uploadAttachmentMetadata);
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
            notificationContents = GetMessageContent(request.Notification, templates, cancellationToken, request.Correspondence.Content?.Language);
            if (notificationContents.Count == 0)
            {
                return NotificationErrors.TemplateNotFound;
            }
            var notificationError = initializeCorrespondenceHelper.ValidateNotification(request.Notification);
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
            return await InitializeCorrespondences(request, attachmentsToBeUploaded, notificationContents, partyUuid, cancellationToken);
        }, logger, cancellationToken);
    }

    private async Task<OneOf<InitializeCorrespondencesResponse, Error>> InitializeCorrespondences(InitializeCorrespondencesRequest request, List<AttachmentEntity> attachmentsToBeUploaded, List<NotificationContent>? notificationContents, Guid partyUuid, CancellationToken cancellationToken)
    {
        var status = initializeCorrespondenceHelper.GetInitializeCorrespondenceStatus(request.Correspondence);
        var correspondences = new List<CorrespondenceEntity>();
        foreach (var recipient in request.Recipients)
        {
            var correspondence = initializeCorrespondenceHelper.MapToCorrespondenceEntity(request, recipient, attachmentsToBeUploaded, status, partyUuid);
            correspondences.Add(correspondence);
        }
        await correspondenceRepository.CreateCorrespondences(correspondences, cancellationToken);

        var initializedCorrespondences = new List<InitializedCorrespondences>();
        foreach (var correspondence in correspondences)
        {
            var dialogJob = backgroundJobClient.Enqueue(() => CreateDialogportenDialog(correspondence));
            if (correspondence.GetHighestStatus()?.Status == CorrespondenceStatus.Initialized || 
                correspondence.GetHighestStatus()?.Status == CorrespondenceStatus.ReadyForPublish) //TODO: Remove ReadyForPublish check if/when ReadyForPublish is removed
            {
                var publishTime = correspondence.RequestedPublishTime;

                if (!hostEnvironment.IsDevelopment())
                {
                    //Adds a 1 minute delay for malware scan to finish if not running locally
                    publishTime = correspondence.RequestedPublishTime.UtcDateTime.AddSeconds(-30) < DateTimeOffset.UtcNow ? DateTimeOffset.UtcNow.AddMinutes(1) : correspondence.RequestedPublishTime.UtcDateTime;
                }

                backgroundJobClient.Schedule<PublishCorrespondenceHandler>((handler) => handler.Process(correspondence.Id, null, cancellationToken), publishTime);

            }
            if (correspondence.DueDateTime is not null)
            {
                backgroundJobClient.Schedule<CorrespondenceDueDateHandler>((handler) => handler.Process(correspondence.Id, cancellationToken), correspondence.DueDateTime.Value);
            }
            await eventBus.Publish(AltinnEventType.CorrespondenceInitialized, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, cancellationToken);

            var notificationDetails = new List<InitializedCorrespondencesNotifications>();
            if (request.Notification != null)
            {
                var notifications = CreateNotifications(request.Notification, correspondence, notificationContents, cancellationToken);
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
                    };
                    notificationDetails.Add(new InitializedCorrespondencesNotifications()
                    {
                        OrderId = entity.NotificationOrderId,
                        IsReminder = entity.IsReminder,
                        Status = notificationOrder.RecipientLookup?.Status == RecipientLookupStatus.Success ? InitializedNotificationStatus.Success : InitializedNotificationStatus.MissingContact
                    });
                    await correspondenceNotificationRepository.AddNotification(entity, cancellationToken);
                    backgroundJobClient.ContinueJobWith<IDialogportenService>(dialogJob, (dialogportenService) => dialogportenService.CreateInformationActivity(correspondence.Id, DialogportenActorType.ServiceOwner, DialogportenTextType.NotificationOrderCreated, notification.RequestedSendTime.ToString("yyyy-MM-dd HH:mm")));
                }
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
            AttachmentIds = correspondences.SelectMany(c => c.Content?.Attachments.Select(a => a.AttachmentId)).ToList()
        };
    }

    private List<NotificationOrderRequest> CreateNotifications(NotificationRequest notification, CorrespondenceEntity correspondence, List<NotificationContent> contents, CancellationToken cancellationToken)
    {
        var notifications = new List<NotificationOrderRequest>();
        string? orgNr = null;
        string? personNr = null;
        NotificationContent? content = null;
        string recipientWithoutPrefix = correspondence.Recipient.WithoutPrefix();
        if (recipientWithoutPrefix.IsOrganizationNumber())
        {
            orgNr = recipientWithoutPrefix;
            content = contents.FirstOrDefault(c => c.RecipientType == RecipientType.Organization || c.RecipientType == null);
        }
        else if (recipientWithoutPrefix.IsSocialSecurityNumber())
        {
            personNr = recipientWithoutPrefix;
            content = contents.FirstOrDefault(c => c.RecipientType == RecipientType.Person || c.RecipientType == null);
        }
        var notificationOrder = new NotificationOrderRequest
        {
            IgnoreReservation = correspondence.IgnoreReservation,
            Recipients = new List<Recipient>{
            new Recipient{
                OrganizationNumber = orgNr,
                NationalIdentityNumber = personNr
            },
        },
            ResourceId = correspondence.ResourceId,
            RequestedSendTime = correspondence.RequestedPublishTime.UtcDateTime <= DateTime.UtcNow ? DateTime.UtcNow.AddMinutes(5) : correspondence.RequestedPublishTime.UtcDateTime.AddMinutes(5),
            SendersReference = correspondence.SendersReference,
            NotificationChannel = notification.NotificationChannel,
            EmailTemplate = new EmailTemplate
            {
                Subject = content.EmailSubject,
                Body = content.EmailBody,
            },
            SmsTemplate = new SmsTemplate
            {
                Body = content.SmsBody,

            }
        };
        notifications.Add(notificationOrder);
        if (notification.SendReminder)
        {
            notifications.Add(new NotificationOrderRequest
            {
                IgnoreReservation = correspondence.IgnoreReservation,
                Recipients = new List<Recipient>
                {
                    new Recipient
                    {
                        OrganizationNumber = orgNr,
                        NationalIdentityNumber = personNr
                    },
                },
                ResourceId = correspondence.ResourceId,
                RequestedSendTime = hostEnvironment.IsProduction() ? notificationOrder.RequestedSendTime.AddDays(7) : notificationOrder.RequestedSendTime.AddHours(1),
                ConditionEndpoint = CreateConditionEndpoint(correspondence.Id.ToString()),
                SendersReference = correspondence.SendersReference,
                NotificationChannel = notification.ReminderNotificationChannel ?? notification.NotificationChannel,
                EmailTemplate = new EmailTemplate
                {
                    Subject = content.ReminderEmailSubject,
                    Body = content.ReminderEmailBody,
                },
                SmsTemplate = new SmsTemplate
                {
                    Body = content.ReminderSmsBody,
                }
            });
        }
        return notifications;
    }
    private List<NotificationContent> GetMessageContent(NotificationRequest request, List<NotificationTemplateEntity> templates, CancellationToken cancellationToken, string? language = null)
    {
        var content = new List<NotificationContent>();
        foreach (var template in templates)
        {
            content.Add(new NotificationContent()
            {
                EmailSubject = CreateMessageFromToken(template.EmailSubject, request.EmailSubject),
                EmailBody = CreateMessageFromToken(template.EmailBody, request.EmailBody),
                SmsBody = CreateMessageFromToken(template.SmsBody, request.SmsBody),
                ReminderEmailBody = CreateMessageFromToken(template.ReminderEmailBody, request.ReminderEmailBody),
                ReminderEmailSubject = CreateMessageFromToken(template.ReminderEmailSubject, request.ReminderEmailSubject),
                ReminderSmsBody = CreateMessageFromToken(template.ReminderSmsBody, request.ReminderSmsBody),
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

    private string CreateMessageFromToken(string message, string? token = "")
    {
        return message.Replace("{textToken}", token + " ").Trim();
    }

    // Must be public to be run by Hangfire
    public async Task CreateDialogportenDialog(CorrespondenceEntity correspondence)
    {
        var dialogId = await dialogportenService.CreateCorrespondenceDialog(correspondence.Id);
        await correspondenceRepository.AddExternalReference(correspondence.Id, ReferenceType.DialogportenDialogId, dialogId);
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
