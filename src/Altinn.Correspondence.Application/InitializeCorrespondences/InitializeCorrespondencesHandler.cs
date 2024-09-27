using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.PublishCorrespondence;
using Altinn.Correspondence.Application.CorrespondenceDueDate;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Core.Services.Enums;
using Hangfire;
using OneOf;
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
    private readonly UserClaimsHelper _userClaimsHelper;

    public InitializeCorrespondencesHandler(InitializeCorrespondenceHelper initializeCorrespondenceHelper, IAltinnAuthorizationService altinnAuthorizationService, IAltinnNotificationService altinnNotificationService, ICorrespondenceRepository correspondenceRepository, ICorrespondenceNotificationRepository correspondenceNotificationRepository, INotificationTemplateRepository notificationTemplateRepository, IEventBus eventBus, IBackgroundJobClient backgroundJobClient, UserClaimsHelper userClaimsHelper)
    {
        _initializeCorrespondenceHelper = initializeCorrespondenceHelper;
        _altinnAuthorizationService = altinnAuthorizationService;
        _altinnNotificationService = altinnNotificationService;
        _correspondenceRepository = correspondenceRepository;
        _correspondenceNotificationRepository = correspondenceNotificationRepository;
        _notificationTemplateRepository = notificationTemplateRepository;
        _eventBus = eventBus;
        _backgroundJobClient = backgroundJobClient;
        _userClaimsHelper = userClaimsHelper;
    }

    public async Task<OneOf<InitializeCorrespondencesResponse, Error>> Process(InitializeCorrespondencesRequest request, CancellationToken cancellationToken)
    {
        var hasAccess = await _altinnAuthorizationService.CheckUserAccess(request.Correspondence.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Send }, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }
        var isSender = _userClaimsHelper.IsSender(request.Correspondence.Sender);
        if (!isSender)
        {
            return Errors.InvalidSender;
        }
        if (request.IsUploadRequest && request.Attachments.Count == 0)
        {
            return Errors.NoAttachments;
        }
        if (request.Recipients.Count != request.Recipients.Distinct().Count())
        {
            return Errors.DuplicateRecipients;
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

        var attachmentError = _initializeCorrespondenceHelper.ValidateAttachmentFiles(request.Attachments, request.Correspondence.Content!.Attachments, request.IsUploadRequest);
        if (attachmentError != null) return attachmentError;
        var attachments = new List<AttachmentEntity>();
        if (request.Correspondence.Content!.Attachments.Count() > 0)
        {
            foreach (var attachment in request.Correspondence.Content!.Attachments)
            {
                var a = await _initializeCorrespondenceHelper.ProcessNewAttachment(attachment, cancellationToken);
                attachments.Add(a);
            }
        }
        if (request.ExistingAttachments.Count > 0)
        {
            var existingAttachments = await _initializeCorrespondenceHelper.GetExistingAttachments(request.ExistingAttachments, cancellationToken);
            if (existingAttachments == null)
            {
                return Errors.ExistingAttachmentNotFound;
            }
            attachments.AddRange(existingAttachments);
        }
        if (request.Attachments.Count > 0)
        {
            var uploadError = await _initializeCorrespondenceHelper.UploadAttachments(attachments, request.Attachments, cancellationToken);
            if (uploadError != null)
            {
                return uploadError;
            }
        }
        List<NotificationContent>? notificationContents = null;
        if (request.Notification != null)
        {
            notificationContents = await getMessageContent(request.Notification, cancellationToken, request.Correspondence.Content?.Language);
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
                    Attachments = attachments.Select(a => new CorrespondenceAttachmentEntity
                    {
                        Attachment = a,
                        Created = DateTimeOffset.UtcNow,

                    }).ToList(),
                    Language = request.Correspondence.Content.Language,
                    MessageBody = request.Correspondence.Content.MessageBody,
                    MessageSummary = request.Correspondence.Content.MessageSummary,
                    MessageTitle = request.Correspondence.Content.MessageTitle,
                },
                VisibleFrom = request.Correspondence.VisibleFrom,
                AllowSystemDeleteAfter = request.Correspondence.AllowSystemDeleteAfter,
                DueDateTime = request.Correspondence.DueDateTime,
                PropertyList = request.Correspondence.PropertyList.ToDictionary(x => x.Key, x => x.Value),
                ReplyOptions = request.Correspondence.ReplyOptions,
                IsReservable = request.Correspondence.IsReservable,
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
            };
            correspondences.Add(correspondence);
        }
        correspondences = await _correspondenceRepository.CreateCorrespondences(correspondences, cancellationToken);
        foreach (var correspondence in correspondences)
        {
            _backgroundJobClient.Schedule<PublishCorrespondenceHandler>((handler) => handler.Process(correspondence.Id, cancellationToken), correspondence.VisibleFrom);
            _backgroundJobClient.Schedule<CorrespondenceDueDateHandler>((handler) => handler.Process(correspondence.Id, cancellationToken), correspondence.DueDateTime);
            await _eventBus.Publish(AltinnEventType.CorrespondenceInitialized, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, cancellationToken);
            if (request.Notification != null)
            {
                var notifications = CreateNotifications(request.Notification, correspondence, notificationContents, cancellationToken);
                foreach (var notification in notifications)
                {
                    var orderId = await _altinnNotificationService.CreateNotification(notification, cancellationToken);
                    var entity = new CorrespondenceNotificationEntity()
                    {
                        Created = DateTime.UtcNow,
                        NotificationChannel = request.Notification.NotificationChannel,
                        NotificationTemplate = request.Notification.NotificationTemplate,
                        CorrespondenceId = correspondence.Id,
                        NotificationOrderId = orderId,
                        RequestedSendTime = notification.RequestedSendTime ?? DateTimeOffset.UtcNow,
                    };
                    await _correspondenceNotificationRepository.AddNotification(entity, cancellationToken);
                }
            }
        }
        return new InitializeCorrespondencesResponse()
        {
            CorrespondenceIds = correspondences.Select(c => c.Id).ToList(),
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
            content = contents.FirstOrDefault(c => c.RecipientType == RecipientType.Person || c.RecipientType == null);
        }
        else if (organizationWithPrefixFormat.IsMatch(correspondence.Recipient))
        {
            orgNr = correspondence.Recipient.Substring(5);
            content = contents.FirstOrDefault(c => c.RecipientType == RecipientType.Person || c.RecipientType == null);
        }
        else if (personFormat.IsMatch(correspondence.Recipient))
        {
            personNr = correspondence.Recipient;
            content = contents.FirstOrDefault(c => c.RecipientType == RecipientType.Person || c.RecipientType == null);
        }
        var notificationOrder = new NotificationOrderRequest
        {
            IgnoreReservation = !correspondence.IsReservable,
            Recipients = new List<Recipient>{
            new Recipient{
                OrganizationNumber = orgNr,
                NationalIdentityNumber = personNr
            },
        },
            ResourceId = correspondence.ResourceId,
            RequestedSendTime = correspondence.VisibleFrom.UtcDateTime.AddMinutes(5),
            ConditionEndpoint = null, // TODO: Implement condition endpoint
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
                IgnoreReservation = !correspondence.IsReservable,
                Recipients = new List<Recipient>{
            new Recipient{
                OrganizationNumber = orgNr,
                NationalIdentityNumber = personNr
            },
        },
                ResourceId = correspondence.ResourceId,
                RequestedSendTime = correspondence.VisibleFrom.UtcDateTime.AddDays(7),
                ConditionEndpoint = null, // TODO: Implement condition endpoint
                SendersReference = correspondence.SendersReference,
                NotificationChannel = notification.NotificationChannel,
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
    private async Task<List<NotificationContent>> getMessageContent(NotificationRequest request, CancellationToken cancellationToken, string? language = null)
    {

        var templates = await _notificationTemplateRepository.GetNotificationTemplates(request.NotificationTemplate, cancellationToken, language);
        if (templates.Count == 0)
        {
            throw new Exception("No notification templates found");
        }
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
    private string CreateMessageFromToken(string message, string? token = "")
    {
        return message.Replace("{textToken}", token + " ").Trim();
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
