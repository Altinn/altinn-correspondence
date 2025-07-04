using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.InitializeCorrespondences;
using Altinn.Correspondence.Application.CheckNotificationDelivery;
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
using Altinn.Correspondence.Common.Constants;

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
    private const int NotificationDeliveryCheckDelayMinutes = 5;

    public async Task Process(CreateNotificationRequest request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting notification creation process for correspondence {CorrespondenceId}", request.CorrespondenceId);
        var correspondence = await correspondenceRepository.GetCorrespondenceById(request.CorrespondenceId, false, true, false, cancellationToken) ?? throw new Exception($"Correspondence with id {request.CorrespondenceId} not found when creating notification");
        try
        {
            // Get notification templates
            logger.LogInformation("Fetching notification templates for template {NotificationTemplate}", request.NotificationRequest.NotificationTemplate);
            var templates = await notificationTemplateRepository.GetNotificationTemplates(
                request.NotificationRequest.NotificationTemplate,
                cancellationToken,
                request.Language);

            if (templates.Count == 0)
            {
                logger.LogError("No notification templates found for template {NotificationTemplate}", request.NotificationRequest.NotificationTemplate);
                throw new Exception($"No notification templates found for template {request.NotificationRequest.NotificationTemplate}");
            }
            logger.LogInformation("Found {TemplateCount} notification templates", templates.Count);

            // Get notification content
            logger.LogInformation("Get notification content for correspondence {CorrespondenceId}", request.CorrespondenceId);
            var notificationContents = await GetNotificationContent(
                request.NotificationRequest,
                templates,
                correspondence,
                cancellationToken,
                request.Language);

            logger.LogInformation("Creating notification V2 for correspondence {CorrespondenceId}", request.CorrespondenceId);
            await CreateNotificationV2(request.NotificationRequest, correspondence, notificationContents, cancellationToken);
            logger.LogInformation("Successfully created notification for correspondence {CorrespondenceId}", request.CorrespondenceId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create notifications for correspondence {CorrespondenceId}", request.CorrespondenceId);
            throw;
        }
    }

    private async Task CreateNotificationsV1(
        NotificationRequest notificationRequest,
        CorrespondenceEntity correspondence,
        List<NotificationContent> notificationContents,
        DateTimeOffset operationTimestamp,
        CancellationToken cancellationToken)
    {
        // Create notification requests
        var notificationRequests = await CreateNotificationRequestsV1(
            notificationRequest,
            correspondence,
            notificationContents,
            cancellationToken);

        foreach (var request in notificationRequests)
        {
            var notificationResponse = await altinnNotificationService.CreateNotification(request, cancellationToken);

            if (notificationResponse is null)
            {
                backgroundJobClient.Enqueue<IEventBus>((eventBus) => eventBus.Publish(AltinnEventType.CorrespondenceNotificationCreationFailed, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, CancellationToken.None));
            }
            else
            {
                var entity = new CorrespondenceNotificationEntity()
                {
                    Created = DateTimeOffset.UtcNow,
                    NotificationChannel = notificationRequest.NotificationChannel,
                    NotificationTemplate = notificationRequest.NotificationTemplate,
                    CorrespondenceId = correspondence.Id,
                    NotificationOrderId = notificationResponse.OrderId,
                    RequestedSendTime = request.RequestedSendTime,
                    IsReminder = request.RequestedSendTime != notificationRequests[0].RequestedSendTime,
                    OrderRequest = JsonSerializer.Serialize(request)
                };

                await correspondenceNotificationRepository.AddNotification(entity, cancellationToken);
                // Create information activity in Dialogporten
                await hangfireScheduleHelper.CreateActivityAfterDialogCreated(correspondence.Id, request, operationTimestamp);

                backgroundJobClient.Enqueue<IEventBus>((eventBus) => eventBus.Publish(AltinnEventType.NotificationCreated, request.ResourceId, notificationResponse.OrderId.ToString(), "notification", correspondence.Sender, CancellationToken.None));
            }
        }
    }

    private async Task<List<NotificationOrderRequest>> CreateNotificationRequestsV1(NotificationRequest notificationRequest, CorrespondenceEntity correspondence, List<NotificationContent> contents, CancellationToken cancellationToken)
    {
        var notificationRequests = new List<NotificationOrderRequest>();
        string recipientWithoutPrefix = correspondence.Recipient.WithoutPrefix();
        bool isOrganization = recipientWithoutPrefix.IsOrganizationNumber();
        bool isPerson = recipientWithoutPrefix.IsSocialSecurityNumber();

        var customRecipient = notificationRequest.CustomRecipient ?? null;
        List<Recipient> relevantRecipients = customRecipient != null ? [customRecipient] :
        [
            new()
            {
                OrganizationNumber = isOrganization ? recipientWithoutPrefix : null,
                NationalIdentityNumber = isPerson ? recipientWithoutPrefix : null
            }
        ];

        NotificationContent? content = null;
        if (isOrganization)
        {
            content = contents.FirstOrDefault(c => c.RecipientType == RecipientType.Organization) ?? contents.FirstOrDefault(c => c.RecipientType == null);
        }
        else if (isPerson)
        {
            content = contents.FirstOrDefault(c => c.RecipientType == RecipientType.Person) ?? contents.FirstOrDefault(c => c.RecipientType == null);
        }
        // await SetRecipientNameOnNotificationContent(content, correspondence.Recipient, cancellationToken);
        var notificationOrderRequest = new NotificationOrderRequest
        {
            IgnoreReservation = correspondence.IgnoreReservation,
            Recipients = relevantRecipients,
            ResourceId = correspondence.ResourceId,
            RequestedSendTime = correspondence.RequestedPublishTime.UtcDateTime <= DateTime.UtcNow ? DateTime.UtcNow.AddMinutes(5) : correspondence.RequestedPublishTime.UtcDateTime.AddMinutes(5),
            SendersReference = correspondence.SendersReference,
            ConditionEndpoint = CreateConditionEndpoint(correspondence.Id.ToString()),
            NotificationChannel = notificationRequest.NotificationChannel,
            EmailTemplate = !string.IsNullOrWhiteSpace(content?.EmailSubject) && !string.IsNullOrWhiteSpace(content.EmailBody) ? new EmailTemplate
            {
                Subject = content.EmailSubject,
                Body = content.EmailBody,
            } : null,
            SmsTemplate = !string.IsNullOrWhiteSpace(content?.SmsBody) ? new SmsTemplate
            {
                Body = content.SmsBody,
            } : null
        };
        notificationRequests.Add(notificationOrderRequest);
        if (notificationRequest.SendReminder)
        {
            notificationRequests.Add(new NotificationOrderRequest
            {
                IgnoreReservation = correspondence.IgnoreReservation,
                Recipients = relevantRecipients,
                ResourceId = correspondence.ResourceId,
                RequestedSendTime = hostEnvironment.IsProduction() ? notificationOrderRequest.RequestedSendTime.AddDays(7) : notificationOrderRequest.RequestedSendTime.AddHours(1),
                ConditionEndpoint = CreateConditionEndpoint(correspondence.Id.ToString()),
                SendersReference = correspondence.SendersReference,
                NotificationChannel = notificationRequest.ReminderNotificationChannel ?? notificationRequest.NotificationChannel,
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
        return notificationRequests;
    }

    private async Task<List<NotificationContent>> GetNotificationContent(NotificationRequest request, List<NotificationTemplateEntity> templates, CorrespondenceEntity correspondence, CancellationToken cancellationToken, string? language = null)
    {
        logger.LogInformation("Getting notification content for correspondence {CorrespondenceId}", correspondence.Id);
        var content = new List<NotificationContent>();
        var sendersName = correspondence.MessageSender;
        if (string.IsNullOrEmpty(sendersName))
        {
            logger.LogInformation("Looking up sender name for {Sender}", correspondence.Sender);
            sendersName = await altinnRegisterService.LookUpName(correspondence.Sender.WithoutPrefix(), cancellationToken);
        }
        logger.LogInformation("Looking up recipient name for {Recipient}", correspondence.Recipient);
        var recipientName = await altinnRegisterService.LookUpName(correspondence.Recipient.WithoutPrefix(), cancellationToken);
        
        foreach (var template in templates)
        {
            logger.LogInformation("Processing template {TemplateId} with language {Language}", template.Id, template.Language);
            content.Add(new NotificationContent()
            {
                EmailSubject = CreateNotificationContentFromToken(template.EmailSubject ?? string.Empty, request.EmailSubject).Replace("$sendersName$", sendersName).Replace("$correspondenceRecipientName$", recipientName),
                EmailBody = CreateNotificationContentFromToken(template.EmailBody ?? string.Empty, request.EmailBody).Replace("$sendersName$", sendersName).Replace("$correspondenceRecipientName$", recipientName),
                SmsBody = CreateNotificationContentFromToken(template.SmsBody ?? string.Empty, request.SmsBody).Replace("$sendersName$", sendersName).Replace("$correspondenceRecipientName$", recipientName),
                ReminderEmailBody = CreateNotificationContentFromToken(template.ReminderEmailBody ?? string.Empty, request.ReminderEmailBody).Replace("$sendersName$", sendersName).Replace("$correspondenceRecipientName$", recipientName),
                ReminderEmailSubject = CreateNotificationContentFromToken(template.ReminderEmailSubject ?? string.Empty, request.ReminderEmailSubject).Replace("$sendersName$", sendersName).Replace("$correspondenceRecipientName$", recipientName),
                ReminderSmsBody = CreateNotificationContentFromToken(template.ReminderSmsBody ?? string.Empty, request.ReminderSmsBody).Replace("$sendersName$", sendersName).Replace("$correspondenceRecipientName$", recipientName),
                Language = template.Language,
                RecipientType = template.RecipientType
            });
        }
        return content;
    }

    private Uri? CreateConditionEndpoint(string correspondenceId)
    {
        var baseUrl = _generalSettings.CorrespondenceBaseUrl.TrimEnd('/');
        var path = $"/correspondence/api/v1/correspondence/{Uri.EscapeDataString(correspondenceId)}/notification/check";
        var conditionEndpoint = new Uri(new Uri(baseUrl), path);

        if (conditionEndpoint.Host == "localhost")
        {
            return null;
        }
        return conditionEndpoint;
    }

    private static string CreateNotificationContentFromToken(string message, string? token = "")
    {
        return message.Replace("{textToken}", token + " ").Trim();
    }

    private NotificationOrderRequestV2 CreateNotificationRequestsV2(NotificationRequest notificationRequest, CorrespondenceEntity correspondence, List<NotificationContent> contents, CancellationToken cancellationToken)
    { 
        logger.LogInformation("Creating notification request V2 for correspondence {CorrespondenceId}", correspondence.Id);
        var notificationOrder = new NotificationOrderRequestV2
        {
            SendersReference = correspondence.SendersReference,
            RequestedSendTime = correspondence.RequestedPublishTime.UtcDateTime <= DateTime.UtcNow
                ? DateTime.UtcNow.AddMinutes(5)
                : correspondence.RequestedPublishTime.UtcDateTime.AddMinutes(5),
            IdempotencyId = Guid.CreateVersion7(),
            Recipient = notificationRequest.CustomRecipient != null
                ? CreateCustomRecipient(notificationRequest, contents.First(), correspondence, isReminder: false)
                : CreateDefaultRecipient(notificationRequest, correspondence, contents, isReminder: false)
        };

        if (notificationRequest.SendReminder)
        {
            notificationOrder.Reminders =
            [
                new ReminderV2
                {
                    SendersReference = correspondence.SendersReference,
                    DelayDays = hostEnvironment.IsProduction() ? 7 : 1,
                    Recipient = notificationRequest.CustomRecipient != null
                        ? CreateCustomRecipient(notificationRequest, contents.First(), correspondence, isReminder: true)
                        : CreateDefaultRecipient(notificationRequest, correspondence, contents, isReminder: true)
                }
            ];
        }
        logger.LogInformation("Notification request V2 created for correspondence {CorrespondenceId}", correspondence.Id);
        return notificationOrder;
    }

    private RecipientV2 CreateCustomRecipient(NotificationRequest notificationRequest, NotificationContent content, CorrespondenceEntity correspondence, bool isReminder)
    {
        if (notificationRequest.CustomRecipient != null)
        {
            var customRecipient = notificationRequest.CustomRecipient;
            var resourceIdWithPrefix = UrnConstants.Resource + ":" + correspondence.ResourceId;
            var channel = isReminder
                ? notificationRequest.ReminderNotificationChannel ?? notificationRequest.NotificationChannel
                : notificationRequest.NotificationChannel;
            var emailSubject = isReminder ? content.ReminderEmailSubject : content.EmailSubject;
            var emailBody = isReminder ? content.ReminderEmailBody : content.EmailBody;
            var smsBody = isReminder ? content.ReminderSmsBody : content.SmsBody;

            var emailSettings = !string.IsNullOrWhiteSpace(emailSubject) && !string.IsNullOrWhiteSpace(emailBody)
                ? new EmailSettings
                {
                    Subject = emailSubject,
                    //For regular emails: Always use EmailContentType
                    //For reminder emails: Try to use ReminderEmailContentType, but if that's not set (is null), 
                    //then use EmailContentType instead
                    Body = emailBody,
                    ContentType = isReminder 
                        ? notificationRequest.ReminderEmailContentType ?? notificationRequest.EmailContentType 
                        : notificationRequest.EmailContentType
                }
                : null;

            var smsSettings = !string.IsNullOrWhiteSpace(smsBody)
                ? new SmsSettings
                {
                    Body = smsBody
                }
                : null;

            if (customRecipient.EmailAddress != null)
            {
                return new RecipientV2
                {
                    RecipientEmail = new RecipientEmail
                    {
                        EmailAddress = customRecipient.EmailAddress,
                        EmailSettings = emailSettings
                    }
                };
            }
            else if (customRecipient.MobileNumber != null)
            {
                return new RecipientV2
                {
                    RecipientSms = new RecipientSms
                    {
                        PhoneNumber = customRecipient.MobileNumber,
                        SmsSettings = smsSettings
                    }
                };
            }
            else if (customRecipient.OrganizationNumber != null)
            {
                return new RecipientV2
                {
                    RecipientOrganization = new RecipientOrganization
                    {
                        OrgNumber = customRecipient.OrganizationNumber,
                        ResourceId = resourceIdWithPrefix,
                        ChannelSchema = channel,
                        EmailSettings = emailSettings,
                        SmsSettings = smsSettings
                    }
                };
            }
            else if (customRecipient.NationalIdentityNumber != null)
            {
                return new RecipientV2
                {
                    RecipientPerson = new RecipientPerson
                    {
                        ResourceId = resourceIdWithPrefix,
                        NationalIdentityNumber = customRecipient.NationalIdentityNumber,
                        ChannelSchema = channel,
                        EmailSettings = emailSettings,
                        SmsSettings = smsSettings
                    }
                };
            }
        }
        return new RecipientV2();
    }

    private RecipientV2 CreateDefaultRecipient(NotificationRequest notificationRequest, CorrespondenceEntity correspondence, List<NotificationContent> contents, bool isReminder)
    {
        string recipientWithoutPrefix = correspondence.Recipient.WithoutPrefix();
        bool isOrganization = recipientWithoutPrefix.IsOrganizationNumber();
        bool isPerson = recipientWithoutPrefix.IsSocialSecurityNumber();

        NotificationContent? content = null;
        if (isOrganization)
        {
            content = contents.FirstOrDefault(c => c.RecipientType == RecipientType.Organization) ?? contents.FirstOrDefault(c => c.RecipientType == null);
        }
        else if (isPerson)
        {
            content = contents.FirstOrDefault(c => c.RecipientType == RecipientType.Person) ?? contents.FirstOrDefault(c => c.RecipientType == null);
        }
        // await SetRecipientNameOnNotificationContent(content, correspondence.Recipient, cancellationToken);

        var resourceIdWithPrefix = UrnConstants.Resource + ":" + correspondence.ResourceId;
        var channel = isReminder
            ? notificationRequest.ReminderNotificationChannel ?? notificationRequest.NotificationChannel
            : notificationRequest.NotificationChannel;
        var emailSubject = isReminder ? content?.ReminderEmailSubject : content?.EmailSubject;
        var emailBody = isReminder ? content?.ReminderEmailBody : content?.EmailBody;
        var smsBody = isReminder ? content?.ReminderSmsBody : content?.SmsBody;

        var emailSettings = !string.IsNullOrWhiteSpace(emailSubject) && !string.IsNullOrWhiteSpace(emailBody)
            ? new EmailSettings
            {
                Subject = emailSubject,
                //For regular emails: Always use EmailContentType
                //For reminder emails: Try to use ReminderEmailContentType, but if that's not set (is null), 
                //then use EmailContentType instead
                Body = emailBody,
                ContentType = isReminder 
                    ? notificationRequest.ReminderEmailContentType ?? notificationRequest.EmailContentType 
                    : notificationRequest.EmailContentType
            }
            : null;

        var smsSettings = !string.IsNullOrWhiteSpace(smsBody)
            ? new SmsSettings
            {
                Body = smsBody
            }
            : null;

        return new RecipientV2
        {
            RecipientOrganization = isOrganization ? new RecipientOrganization
            {
                OrgNumber = recipientWithoutPrefix,
                ResourceId = resourceIdWithPrefix,
                ChannelSchema = channel,
                EmailSettings = emailSettings,
                SmsSettings = smsSettings
            } : null,
            RecipientPerson = isPerson ? new RecipientPerson
            {
                ResourceId = resourceIdWithPrefix,
                NationalIdentityNumber = recipientWithoutPrefix,
                ChannelSchema = channel,
                EmailSettings = emailSettings,
                SmsSettings = smsSettings
            } : null
        };
    }

    private async Task CreateNotificationV2(
        NotificationRequest notificationRequest,
        CorrespondenceEntity correspondence,
        List<NotificationContent> notificationContents,
        CancellationToken cancellationToken)
    {
        logger.LogInformation("Creating notification in Altinn Notification Service (v2) for correspondence {CorrespondenceId}", correspondence.Id);
        // Create notification request
        var notificationRequestV2 = CreateNotificationRequestsV2(
            notificationRequest,
            correspondence,
            notificationContents,
            cancellationToken);

        logger.LogInformation("Sending notification request V2 to notification service for correspondence {CorrespondenceId}", correspondence.Id);
        var notificationResponse = await altinnNotificationService.CreateNotificationV2(notificationRequestV2, cancellationToken);

        if (notificationResponse is null)
        {
            logger.LogError("Failed to create notification V2 for correspondence {CorrespondenceId}", correspondence.Id);
            backgroundJobClient.Enqueue<IEventBus>((eventBus) => eventBus.Publish(AltinnEventType.CorrespondenceNotificationCreationFailed, correspondence.ResourceId, correspondence.Id.ToString(), "correspondence", correspondence.Sender, CancellationToken.None));
        }
        else
        {
            logger.LogInformation("Successfully created notification V2 for correspondence {CorrespondenceId} with order ID {OrderId}", correspondence.Id, notificationResponse.NotificationOrderId);
            var notification = new CorrespondenceNotificationEntity()
            {
                Created = DateTimeOffset.UtcNow,
                NotificationChannel = notificationRequest.NotificationChannel,
                NotificationTemplate = notificationRequest.NotificationTemplate,
                CorrespondenceId = correspondence.Id,
                NotificationOrderId = notificationResponse.NotificationOrderId,
                RequestedSendTime = notificationRequestV2.RequestedSendTime,
                IsReminder = false,
                OrderRequest = JsonSerializer.Serialize(notificationRequestV2),
                ShipmentId = notificationResponse.Notification.ShipmentId
            };

            logger.LogInformation("Adding notification entity to database for correspondence {CorrespondenceId}", correspondence.Id);
            await correspondenceNotificationRepository.AddNotification(notification, cancellationToken);
            if (notificationRequest.SendReminder)
            {
                logger.LogInformation("Creating reminder notification entity to database for correspondence {CorrespondenceId}", correspondence.Id);
                var reminder = new CorrespondenceNotificationEntity()
                {
                    Created = DateTimeOffset.UtcNow,
                    NotificationChannel = notificationRequest.ReminderNotificationChannel ?? notificationRequest.NotificationChannel,
                    NotificationTemplate = notificationRequest.NotificationTemplate,
                    CorrespondenceId = correspondence.Id,
                    NotificationOrderId = notificationResponse.NotificationOrderId,
                    RequestedSendTime = notificationRequestV2.RequestedSendTime.AddDays(notificationRequestV2.Reminders?.FirstOrDefault()?.DelayDays ?? 0),
                    IsReminder = true,
                    OrderRequest = JsonSerializer.Serialize(notificationRequestV2),
                    ShipmentId = notificationResponse.Notification.Reminders.FirstOrDefault()?.ShipmentId
                };
                await correspondenceNotificationRepository.AddNotification(reminder, cancellationToken);

                // Schedule notification delivery check for reminder
                logger.LogInformation("Scheduling notification delivery check for reminder notification {NotificationId}", reminder.Id);
                backgroundJobClient.Schedule<CheckNotificationDeliveryHandler>(
                    handler => handler.Process(reminder.Id, cancellationToken),
                    reminder.RequestedSendTime.AddMinutes(NotificationDeliveryCheckDelayMinutes));
            }
            

            // Schedule notification delivery check for main notification
            logger.LogInformation("Scheduling notification delivery check for main notification {NotificationId}", notification.Id);
            backgroundJobClient.Schedule<CheckNotificationDeliveryHandler>(
                handler => handler.Process(notification.Id, cancellationToken),
                notification.RequestedSendTime.AddMinutes(NotificationDeliveryCheckDelayMinutes));

            logger.LogInformation("Publishing notification created event for correspondence {CorrespondenceId}", correspondence.Id);
            backgroundJobClient.Enqueue<IEventBus>((eventBus) => eventBus.Publish(AltinnEventType.NotificationCreated, correspondence.ResourceId, notificationResponse.NotificationOrderId.ToString(), "notification", correspondence.Sender, CancellationToken.None));
        }
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
