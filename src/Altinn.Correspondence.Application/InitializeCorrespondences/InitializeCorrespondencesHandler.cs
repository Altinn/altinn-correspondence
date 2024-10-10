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
using OneOf;
using System.Text.RegularExpressions;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;

namespace Altinn.Correspondence.Application.InitializeCorrespondences;

public class InitializeCorrespondencesHandler : IHandler<InitializeCorrespondencesRequest, InitializeCorrespondencesResponse>
{
    private readonly IAltinnAuthorizationService _altinnAuthorizationService;
    private readonly IAltinnNotificationService _altinnNotificationService;
    private readonly IOptions<AltinnOptions> _altinnOptions;
    private readonly ICorrespondenceRepository _correspondenceRepository;
    private readonly ICorrespondenceNotificationRepository _correspondenceNotificationRepository;
    private readonly INotificationTemplateRepository _notificationTemplateRepository;
    private readonly IAttachmentRepository _attachmentRepository;
    private readonly IEventBus _eventBus;
    private readonly InitializeCorrespondenceHelper _initializeCorrespondenceHelper;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IDialogportenService _dialogportenService;
    private readonly UserClaimsHelper _userClaimsHelper;
    private readonly IHostEnvironment _hostEnvironment;

    public InitializeCorrespondencesHandler(IOptions<AltinnOptions> altinnOptions, InitializeCorrespondenceHelper initializeCorrespondenceHelper, IAltinnAuthorizationService altinnAuthorizationService, IAltinnNotificationService altinnNotificationService, ICorrespondenceRepository correspondenceRepository, ICorrespondenceNotificationRepository correspondenceNotificationRepository, INotificationTemplateRepository notificationTemplateRepository, IAttachmentRepository attachmentRepository, IEventBus eventBus, IBackgroundJobClient backgroundJobClient, UserClaimsHelper userClaimsHelper, IDialogportenService dialogportenService, IHostEnvironment hostEnvironment)
    {
        _altinnOptions = altinnOptions;
        _initializeCorrespondenceHelper = initializeCorrespondenceHelper;
        _altinnAuthorizationService = altinnAuthorizationService;
        _altinnNotificationService = altinnNotificationService;
        _correspondenceRepository = correspondenceRepository;
        _correspondenceNotificationRepository = correspondenceNotificationRepository;
        _notificationTemplateRepository = notificationTemplateRepository;
        _attachmentRepository = attachmentRepository;
        _eventBus = eventBus;
        _backgroundJobClient = backgroundJobClient;
        _dialogportenService = dialogportenService;
        _userClaimsHelper = userClaimsHelper;
        _hostEnvironment = hostEnvironment;
    }

    public async Task<OneOf<InitializeCorrespondencesResponse, Error>> Process(InitializeCorrespondencesRequest request, CancellationToken cancellationToken)
    {
        var hasAccess = await _altinnAuthorizationService.CheckUserAccess(request.Correspondence.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Write }, cancellationToken);
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
        bool isUploadRequest = request.IsUploadRequest;
        var existingAttachmentIds = request.ExistingAttachments;
        var uploadAttachments = request.Attachments;
        var uploadAttachmentMetadata = request.Correspondence.Content.Attachments;

        if (isUploadRequest && uploadAttachments.Count == 0)
        {
            return Errors.NoAttachments;
        }
        // Validate that existing attachments are correct
        var existingAttachments = await _initializeCorrespondenceHelper.GetExistingAttachments(existingAttachmentIds);
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
                VisibleFrom = request.Correspondence.VisibleFrom,
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
            };
            correspondences.Add(correspondence);
        }
        correspondences = await _correspondenceRepository.CreateCorrespondences(correspondences, cancellationToken);
        foreach (var correspondence in correspondences)
        {
            var dialogId = await _dialogportenService.CreateCorrespondenceDialog(correspondence.Id, cancellationToken);
            await _correspondenceRepository.AddExternalReference(correspondence.Id, ReferenceType.DialogportenDialogId, dialogId, cancellationToken);
            if (correspondence.GetLatestStatus()?.Status != CorrespondenceStatus.Published)
            {
                var publishTime = correspondence.VisibleFrom;

                if (!_hostEnvironment.IsDevelopment())
                {
                    //Adds a 1 minute delay for malware scan to finish if not running locally
                    publishTime = correspondence.VisibleFrom.UtcDateTime.AddSeconds(-30) < DateTime.UtcNow ? DateTime.UtcNow.AddMinutes(1) : correspondence.VisibleFrom.UtcDateTime;
                }

                _backgroundJobClient.Schedule<PublishCorrespondenceHandler>((handler) => handler.Process(correspondence.Id, cancellationToken), publishTime);

            }
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
                        IsReminder = notification.RequestedSendTime != notifications[0].RequestedSendTime,
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
            RequestedSendTime = correspondence.VisibleFrom.UtcDateTime.AddMinutes(5),
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
                Recipients = new List<Recipient>{
            new Recipient{
                OrganizationNumber = orgNr,
                NationalIdentityNumber = personNr
            },
        },
                ResourceId = correspondence.ResourceId,
                RequestedSendTime = correspondence.VisibleFrom.UtcDateTime.AddDays(7),
                ConditionEndpoint = CreateConditonEndpoint(correspondence.Id.ToString()),
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
    private Uri CreateConditonEndpoint(string correspondenceId)
    {
        return new Uri($"{_altinnOptions.Value.PlatformGatewayUrl}correspondence/api/v1/correspondence/{correspondenceId}/notification/check");
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
