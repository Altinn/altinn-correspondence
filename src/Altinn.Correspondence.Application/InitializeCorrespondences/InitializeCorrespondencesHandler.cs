using Altinn.Correspondence.Application.CorrespondenceDueDate;
using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.PublishCorrespondence;
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
    private readonly IEventBus _eventBus;
    private readonly InitializeCorrespondenceHelper _initializeCorrespondenceHelper;
    private readonly IBackgroundJobClient _backgroundJobClient;
    private readonly IDialogportenService _dialogportenService;
    private readonly UserClaimsHelper _userClaimsHelper;

    public InitializeCorrespondencesHandler(InitializeCorrespondenceHelper initializeCorrespondenceHelper, IAltinnAuthorizationService altinnAuthorizationService, IAltinnNotificationService altinnNotificationService, ICorrespondenceRepository correspondenceRepository, ICorrespondenceNotificationRepository correspondenceNotificationRepository, IEventBus eventBus, IBackgroundJobClient backgroundJobClient, UserClaimsHelper userClaimsHelper, IDialogportenService dialogportenService)
    {
        _initializeCorrespondenceHelper = initializeCorrespondenceHelper;
        _altinnAuthorizationService = altinnAuthorizationService;
        _altinnNotificationService = altinnNotificationService;
        _correspondenceRepository = correspondenceRepository;
        _correspondenceNotificationRepository = correspondenceNotificationRepository;
        _eventBus = eventBus;
        _backgroundJobClient = backgroundJobClient;
        _dialogportenService = dialogportenService;
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
                Notifications = _initializeCorrespondenceHelper.ProcessNotifications(request.Correspondence.Notifications, cancellationToken),
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
            if (correspondence.GetLatestStatus()?.Status != CorrespondenceStatus.Published) { 
                _backgroundJobClient.Schedule<PublishCorrespondenceHandler>((handler) => handler.Process(correspondence.Id, cancellationToken), correspondence.VisibleFrom);
            }
            _backgroundJobClient.Schedule<CorrespondenceDueDateHandler>((handler) => handler.Process(correspondence.Id, cancellationToken), correspondence.DueDateTime);
            await _eventBus.Publish(AltinnEventType.CorrespondenceInitialized, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, cancellationToken);
            if (request.Notification != null)
            {
                var notifications = CreateNotifications(request.Notification, correspondence);
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
                    await _dialogportenService.CreateInformationActivity(correspondence.Id, DialogportenActorType.ServiceOwner, $"Opprettet varslingsordre for tidspunkt {entity.RequestedSendTime}", cancellationToken: cancellationToken);
                }
            }
        }
        return new InitializeCorrespondencesResponse()
        {
            CorrespondenceIds = correspondences.Select(c => c.Id).ToList(),
            AttachmentIds = correspondences.SelectMany(c => c.Content?.Attachments.Select(a => a.AttachmentId)).ToList()
        };
    }

    private List<NotificationOrderRequest> CreateNotifications(NotificationRequest notification, CorrespondenceEntity correspondence)
    {
        var notifications = new List<NotificationOrderRequest>();

        var organizationWithoutPrefixFormat = new Regex(@"^\d{9}$");
        var organizationWithPrefixFormat = new Regex(@"^\d{4}:\d{9}$");
        var personFormat = new Regex(@"^\d{11}$");
        string? orgNr = null;
        string? personNr = null;
        if (organizationWithoutPrefixFormat.IsMatch(correspondence.Recipient))
        {
            orgNr = correspondence.Recipient;
        }
        else if (organizationWithPrefixFormat.IsMatch(correspondence.Recipient))
        {
            orgNr = correspondence.Recipient.Substring(5);
        }
        else if (personFormat.IsMatch(correspondence.Recipient))
        {
            personNr = correspondence.Recipient;
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
            RequestedSendTime = correspondence.VisibleFrom.UtcDateTime,
            ConditionEndpoint = null, // TODO: Implement condition endpoint
            SendersReference = correspondence.SendersReference,
            NotificationChannel = notification.NotificationChannel,
            EmailTemplate = new EmailTemplate
            {
                Subject = notification.EmailSubject,
                Body = notification.EmailBody,
            },
            SmsTemplate = new SmsTemplate
            {
                Body = notification.SmsBody,

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
                    Subject = notification.ReminderEmailSubject,
                    Body = notification.ReminderEmailBody,
                },
                SmsTemplate = new SmsTemplate
                {
                    Body = notification.ReminderSmsBody,
                }
            });
        }
        return notifications;
    }
}
