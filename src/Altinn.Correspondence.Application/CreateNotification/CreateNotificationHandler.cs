using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.InitializeCorrespondences;
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
using System.Text.Json;

namespace Altinn.Correspondence.Application.CreateNotification;

public class CreateNotificationHandler(
    IAltinnNotificationService altinnNotificationService,
    IAltinnRegisterService altinnRegisterService,
    ICorrespondenceRepository correspondenceRepository,
    ICorrespondenceNotificationRepository correspondenceNotificationRepository,
    INotificationTemplateRepository notificationTemplateRepository,
    IBackgroundJobClient backgroundJobClient,
    IHostEnvironment hostEnvironment,
    HangfireScheduleHelper hangfireScheduleHelper,
    IOptions<GeneralSettings> generalSettings,
    ILogger<CreateNotificationHandler> logger)
{
    private readonly GeneralSettings _generalSettings = generalSettings.Value;

    public async Task Process(NotificationRequest request, Guid correspondenceId, CancellationToken cancellationToken)
    {
        var correspondence = await correspondenceRepository.GetCorrespondenceById(correspondenceId, false, false, false, cancellationToken);
        
        try
        {
            // Get notification templates
            var templates = await notificationTemplateRepository.GetNotificationTemplates(
                request.NotificationTemplate, 
                cancellationToken, 
                correspondence.Content?.Language);

            if (templates.Count == 0)
            {
                throw new Exception($"No notification templates found for template {request.NotificationTemplate}");
            }

            // Get notification content
            var notificationContents = await GetNotificationContent(
                request, 
                templates, 
                correspondence, 
                cancellationToken, 
                correspondence.Content?.Language);

            // Create notifications
            var notificationRequests = await CreateNotificationRequests(
                request, 
                correspondence, 
                notificationContents, 
                cancellationToken);

            foreach (var notificationRequest in notificationRequests)
            {
                var notificationResponse = await altinnNotificationService.CreateNotification(notificationRequest, cancellationToken);
                
                if (notificationResponse is null )
                {
                    backgroundJobClient.Enqueue<IEventBus>((eventBus) => eventBus.Publish(AltinnEventType.CorrespondenceNotificationCreationFailed, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, CancellationToken.None));
                }
                else {
                    var entity = new CorrespondenceNotificationEntity()
                    {
                        Created = DateTimeOffset.UtcNow,
                        NotificationChannel = request.NotificationChannel,
                        NotificationTemplate = request.NotificationTemplate,
                        CorrespondenceId = correspondence.Id,
                        NotificationOrderId = notificationResponse.OrderId,
                        RequestedSendTime = notificationRequest.RequestedSendTime,
                        IsReminder = notificationRequest.RequestedSendTime != notificationRequests[0].RequestedSendTime,
                        OrderRequest = JsonSerializer.Serialize(notificationRequest)
                    };

                    await correspondenceNotificationRepository.AddNotification(entity, cancellationToken);
                    // Create information activity in Dialogporten
                    await hangfireScheduleHelper.CreateActivityAfterDialogCreated(correspondence, notificationRequest);

                    backgroundJobClient.Enqueue<IEventBus>((eventBus) => eventBus.Publish(AltinnEventType.NotificationCreated, notificationRequest.ResourceId, notificationResponse.OrderId.ToString(), "notification", correspondence.Sender, CancellationToken.None));        
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create notifications for correspondence {CorrespondenceId}", correspondenceId);
            throw;
        }
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

    private async Task<List<NotificationOrderRequest>> CreateNotificationRequests(NotificationRequest notification, CorrespondenceEntity correspondence, List<NotificationContent> contents, CancellationToken cancellationToken)
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
