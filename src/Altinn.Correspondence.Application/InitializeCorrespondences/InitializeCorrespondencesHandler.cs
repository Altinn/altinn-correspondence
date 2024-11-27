using Altinn.Correspondence.Application.CorrespondenceDueDate;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.PublishCorrespondence;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Hangfire;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using OneOf;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace Altinn.Correspondence.Application.InitializeCorrespondences;

public class InitializeCorrespondencesHandler : IHandler<InitializeCorrespondencesRequest, InitializeCorrespondencesResponse>
{
    private readonly IAltinnAuthorizationService _altinnAuthorizationService;
    private readonly IAltinnNotificationService _altinnNotificationService;
    private readonly ICorrespondenceRepository _correspondenceRepository;
    private readonly ICorrespondenceNotificationRepository _correspondenceNotificationRepository;
    private readonly INotificationTemplateRepository _notificationTemplateRepository;
    private readonly IEventBus _eventBus;
    private readonly InitializeCorrespondenceHelper _initializeCorrespondenceHelper;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IDialogportenService _dialogportenService;
    private readonly UserClaimsHelper _userClaimsHelper;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly GeneralSettings _generalSettings;

    public InitializeCorrespondencesHandler(InitializeCorrespondenceHelper initializeCorrespondenceHelper, IAltinnAuthorizationService altinnAuthorizationService, IAltinnNotificationService altinnNotificationService, ICorrespondenceRepository correspondenceRepository, ICorrespondenceNotificationRepository correspondenceNotificationRepository, INotificationTemplateRepository notificationTemplateRepository, IEventBus eventBus, IBackgroundJobClient backgroundJobClient, UserClaimsHelper userClaimsHelper, IDialogportenService dialogportenService, IHostEnvironment hostEnvironment, IOptions<GeneralSettings> generalSettings)
    {
        _initializeCorrespondenceHelper = initializeCorrespondenceHelper;
        _altinnAuthorizationService = altinnAuthorizationService;
        _altinnNotificationService = altinnNotificationService;
        _correspondenceRepository = correspondenceRepository;
        _correspondenceNotificationRepository = correspondenceNotificationRepository;
        _notificationTemplateRepository = notificationTemplateRepository;
        _eventBus = eventBus;
        _backgroundJobClient = backgroundJobClient;
        _dialogportenService = dialogportenService;
        _userClaimsHelper = userClaimsHelper;
        _hostEnvironment = hostEnvironment;
        _generalSettings = generalSettings.Value;
    }

    public async Task<OneOf<InitializeCorrespondencesResponse, Error>> Process(InitializeCorrespondencesRequest request, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        var hasAccess = await _altinnAuthorizationService.CheckUserAccess(user, request.Correspondence.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Write }, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }
        var isSender = _userClaimsHelper.IsSender(request.Correspondence.Sender);
        if (!isSender)
        {
            return Errors.InvalidSender;
        }
        if (request.Recipients.Count != request.Recipients.Distinct().Count())
        {
            return Errors.DuplicateRecipients;
        }
        if (request.Correspondence.IsConfirmationNeeded && request.Correspondence.DueDateTime is null)
        {
            return Errors.DueDateRequired;
        }
        var dateError = _initializeCorrespondenceHelper.ValidateDateConstraints(request.Correspondence);
        if (dateError != null)
        {
            return dateError;
        }
        var contentError = _initializeCorrespondenceHelper.ValidateCorrespondenceContent(request.Correspondence.Content);
        if (contentError != null)
        {
            return contentError;
        }
        var existingAttachmentIds = request.ExistingAttachments;
        var uploadAttachments = request.Attachments;
        var uploadAttachmentMetadata = request.Correspondence.Content.Attachments;

        // Validate that existing attachments are correct
        var getExistingAttachments = await _initializeCorrespondenceHelper.GetExistingAttachments(existingAttachmentIds, request.Correspondence.Sender);
        if (getExistingAttachments.IsT1) return getExistingAttachments.AsT1;
        var existingAttachments = getExistingAttachments.AsT0;
        if (existingAttachments.Count != existingAttachmentIds.Count)
        {
            return Errors.ExistingAttachmentNotFound;
        }
        // Validate that existing attachments are published
        var anyExistingAttachmentsNotPublished = existingAttachments.Any(a => a.GetLatestStatus()?.Status != AttachmentStatus.Published);
        if (anyExistingAttachmentsNotPublished)
        {
            return Errors.AttachmentNotPublished;
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
                var processedAttachment = await _initializeCorrespondenceHelper.ProcessNewAttachment(attachment, cancellationToken);
                attachmentsToBeUploaded.Add(processedAttachment);
            }
        }
        if (existingAttachmentIds.Count > 0)
        {
            attachmentsToBeUploaded.AddRange(existingAttachments.Where(a => a != null).Select(a => a!));
        }
        // Upload attachments
        if (uploadAttachments.Count > 0)
        {
            var uploadError = await _initializeCorrespondenceHelper.UploadAttachments(attachmentsToBeUploaded, uploadAttachments, cancellationToken);
            if (uploadError != null)
            {
                return uploadError;
            }
        }
        List<NotificationContent>? notificationContents = null;
        List<NotificationTemplateEntity>? templates = null;
        if (request.Notification != null)
        {
            templates = await _notificationTemplateRepository.GetNotificationTemplates(request.Notification.NotificationTemplate, cancellationToken, request.Correspondence.Content?.Language);
            if (templates.Count == 0)
            {
                return Errors.NotificationTemplateNotFound;
            }
            notificationContents = GetMessageContent(request.Notification, templates, cancellationToken, request.Correspondence.Content?.Language);
            if (notificationContents.Count == 0)
            {
                return Errors.NotificationTemplateNotFound;
            }
            var notificationError = _initializeCorrespondenceHelper.ValidateNotification(request.Notification);
            if (notificationError != null)
            {
                return notificationError;
            }
        }

        var status = _initializeCorrespondenceHelper.GetInitializeCorrespondenceStatus(request.Correspondence);
        var correspondences = new List<CorrespondenceEntity>();
        foreach (var recipient in request.Recipients)
        {
            var correspondence = new CorrespondenceEntity
            {
                ResourceId = request.Correspondence.ResourceId,
                Recipient = recipient,
                Sender = request.Correspondence.Sender,
                SendersReference = request.Correspondence.SendersReference,
                MessageSender = request.Correspondence.MessageSender,
                Content = new CorrespondenceContentEntity
                {
                    Attachments = attachmentsToBeUploaded.Select(a => new CorrespondenceAttachmentEntity
                    {
                        Attachment = a,
                        Created = DateTimeOffset.UtcNow,
                    }).ToList(),
                    Language = request.Correspondence.Content.Language,
                    MessageBody = request.Correspondence.Content.MessageBody,
                    MessageSummary = request.Correspondence.Content.MessageSummary,
                    MessageTitle = request.Correspondence.Content.MessageTitle,
                },
                RequestedPublishTime = request.Correspondence.RequestedPublishTime,
                AllowSystemDeleteAfter = request.Correspondence.AllowSystemDeleteAfter,
                DueDateTime = request.Correspondence.DueDateTime,
                PropertyList = request.Correspondence.PropertyList.ToDictionary(x => x.Key, x => x.Value),
                ReplyOptions = request.Correspondence.ReplyOptions,
                IgnoreReservation = request.Correspondence.IgnoreReservation,
                Statuses = new List<CorrespondenceStatusEntity>(){
                    new CorrespondenceStatusEntity
                    {
                        Status = status,
                        StatusChanged = DateTimeOffset.UtcNow,
                        StatusText = status.ToString()
                    }
                },
                Created = request.Correspondence.Created,
                ExternalReferences = request.Correspondence.ExternalReferences,
                Published = status == CorrespondenceStatus.Published ? DateTimeOffset.UtcNow : null,
                IsConfirmationNeeded = request.Correspondence.IsConfirmationNeeded,
            };
            correspondences.Add(correspondence);
        }
        await _correspondenceRepository.CreateCorrespondences(correspondences, cancellationToken);

        var initializedCorrespondences = new List<InitializedCorrespondences>();
        foreach (var correspondence in correspondences)
        {
            var dialogJob = _backgroundJobClient.Enqueue(() => CreateDialogportenDialog(correspondence));
            if (correspondence.GetLatestStatus()?.Status != CorrespondenceStatus.Published)
            {
                var publishTime = correspondence.RequestedPublishTime;

                if (!_hostEnvironment.IsDevelopment())
                {
                    //Adds a 1 minute delay for malware scan to finish if not running locally
                    publishTime = correspondence.RequestedPublishTime.UtcDateTime.AddSeconds(-30) < DateTimeOffset.UtcNow ? DateTimeOffset.UtcNow.AddMinutes(1) : correspondence.RequestedPublishTime.UtcDateTime;
                }

                _backgroundJobClient.Schedule<PublishCorrespondenceHandler>((handler) => handler.Process(correspondence.Id, null, cancellationToken), publishTime);

            }
            if (correspondence.DueDateTime is not null)
            {
                _backgroundJobClient.Schedule<CorrespondenceDueDateHandler>((handler) => handler.Process(correspondence.Id, cancellationToken), correspondence.DueDateTime.Value);
            }
            await _eventBus.Publish(AltinnEventType.CorrespondenceInitialized, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, cancellationToken);

            var notificationDetails = new List<InitializedCorrespondencesNotifications>();
            if (request.Notification != null)
            {
                var notifications = CreateNotifications(request.Notification, correspondence, notificationContents, cancellationToken);
                foreach (var notification in notifications)
                {
                    var notificationOrder = await _altinnNotificationService.CreateNotification(notification, cancellationToken);
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
                        RequestedSendTime = notification.RequestedSendTime ?? DateTimeOffset.UtcNow,
                        IsReminder = notification.RequestedSendTime != notifications[0].RequestedSendTime,
                    };
                    notificationDetails.Add(new InitializedCorrespondencesNotifications()
                    {
                        OrderId = entity.NotificationOrderId,
                        IsReminder = entity.IsReminder,
                        Status = notificationOrder.RecipientLookup?.Status == RecipientLookupStatus.Success ? InitializedNotificationStatus.Success : InitializedNotificationStatus.MissingContact
                    });
                    await _correspondenceNotificationRepository.AddNotification(entity, cancellationToken);
                    _backgroundJobClient.ContinueJobWith<IDialogportenService>(dialogJob, (dialogportenService) => dialogportenService.CreateInformationActivity(correspondence.Id, DialogportenActorType.ServiceOwner, DialogportenTextType.NotificationOrderCreated, notification.RequestedSendTime!.Value.ToString("yyyy-MM-dd HH:mm")));
                }
            }
            initializedCorrespondences.Add(new InitializedCorrespondences()
            {
                CorrespondenceId = correspondence.Id,
                Status = correspondence.GetLatestStatus().Status,
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

        var organizationWithoutPrefixFormat = new Regex(@"^\d{9}$");
        var organizationWithPrefixFormat = new Regex(@"^\d{4}:\d{9}$");
        var personFormat = new Regex(@"^\d{11}$");
        string? orgNr = null;
        string? personNr = null;
        NotificationContent? content = null;
        if (organizationWithoutPrefixFormat.IsMatch(correspondence.Recipient))
        {
            orgNr = correspondence.Recipient;
            content = contents.FirstOrDefault(c => c.RecipientType == RecipientType.Organization || c.RecipientType == null);
        }
        else if (organizationWithPrefixFormat.IsMatch(correspondence.Recipient))
        {
            orgNr = correspondence.Recipient.Substring(5);
            content = contents.FirstOrDefault(c => c.RecipientType == RecipientType.Organization || c.RecipientType == null);
        }
        else if (personFormat.IsMatch(correspondence.Recipient))
        {
            personNr = correspondence.Recipient;
            content = contents.FirstOrDefault(c => c.RecipientType == RecipientType.Person || c.RecipientType == null);
        }

        List<Recipient> recipients = new List<Recipient>();

        if (notification.Recipients.Count == 0)
        {
            recipients.Add(new Recipient
            {
                OrganizationNumber = orgNr,
                NationalIdentityNumber = personNr
            });
        }
        else recipients = notification.Recipients;
        var notificationOrder = new NotificationOrderRequest
        {
            IgnoreReservation = correspondence.IgnoreReservation,
            Recipients = recipients,
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
                Recipients = recipients,
                ResourceId = correspondence.ResourceId,
                RequestedSendTime = _hostEnvironment.IsProduction() ? notificationOrder.RequestedSendTime.Value.AddDays(7) : notificationOrder.RequestedSendTime.Value.AddHours(1),
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
        var dialogId = await _dialogportenService.CreateCorrespondenceDialog(correspondence.Id);
        await _correspondenceRepository.AddExternalReference(correspondence.Id, ReferenceType.DialogportenDialogId, dialogId);
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
