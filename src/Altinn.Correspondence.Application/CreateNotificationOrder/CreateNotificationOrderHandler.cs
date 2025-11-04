using System.Text.Json;
using Altinn.Correspondence.Application.InitializeCorrespondences;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;

namespace Altinn.Correspondence.Application.CreateNotificationOrder;

public class CreateNotificationOrderHandler(
    ICorrespondenceRepository correspondenceRepository,
    IAltinnRegisterService altinnRegisterService,
    INotificationTemplateRepository notificationTemplateRepository,
    ICorrespondenceNotificationRepository correspondenceNotificationRepository,
    IIdempotencyKeyRepository idempotencyKeyRepository,
    IHostEnvironment hostEnvironment,
    IOptions<GeneralSettings> generalSettings,
    ILogger<CreateNotificationOrderHandler> logger)
{
    private readonly GeneralSettings _generalSettings = generalSettings.Value;

    public async Task Process(CreateNotificationOrderRequest request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting notification order creation for correspondence {CorrespondenceId}", request.CorrespondenceId);
        var correspondence = await correspondenceRepository.GetCorrespondenceById(request.CorrespondenceId, false, true, false, cancellationToken) ?? throw new Exception($"Correspondence with id {request.CorrespondenceId} not found when creating notification order");
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
            logger.LogInformation("Getting notification content for correspondence {CorrespondenceId}", request.CorrespondenceId);
            var notificationContents = await GetNotificationContent(
                request.NotificationRequest,
                templates,
                correspondence,
                cancellationToken,
                request.Language);
            
            // Persist notification order requests
            await PersistNotificationOrderRequests(request.NotificationRequest, correspondence, notificationContents, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create notification order for correspondence {CorrespondenceId}", request.CorrespondenceId);
            throw;
        }
    }

    private async Task<List<NotificationContent>> GetNotificationContent(NotificationRequest request, List<NotificationTemplateEntity> templates, CorrespondenceEntity correspondence, CancellationToken cancellationToken, string? language = null)
    {
        var content = new List<NotificationContent>();
        var sendersName = correspondence.MessageSender;
        if (string.IsNullOrEmpty(sendersName))
        {
            logger.LogInformation("Looking up sender name for correspondence {CorrespondenceId}", correspondence.Id);
            sendersName = await altinnRegisterService.LookUpName(correspondence.Sender.WithoutPrefix(), cancellationToken);
        }
        logger.LogInformation("Looking up recipient name for correspondence {CorrespondenceId}", correspondence.Id);
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

    private static string CreateNotificationContentFromToken(string message, string? token = "")
    {
        return message.Replace("{textToken}", token + " ").Trim();
    }

    private List<NotificationOrderRequestV2> CreateNotificationOrderRequestsV2(NotificationRequest notificationRequest, CorrespondenceEntity correspondence, List<NotificationContent> contents, CancellationToken cancellationToken)
    { 
        logger.LogInformation("Creating notification order request V2 for correspondence {CorrespondenceId}", correspondence.Id);
        
        // Determine recipients to process - behavior depends on OverrideRegisteredContactInformation flag
        List<Recipient> recipientsToProcess = new List<Recipient>();
        
        // If OverrideRegisteredContactInformation is false (default), add the default correspondence recipient
        if (!notificationRequest.OverrideRegisteredContactInformation)
        {
            string recipientWithoutPrefix = correspondence.Recipient.WithoutPrefix();
            bool isOrganization = recipientWithoutPrefix.IsOrganizationNumber();
            bool isPerson = recipientWithoutPrefix.IsSocialSecurityNumber();
            
            recipientsToProcess.Add(new Recipient
            {
                OrganizationNumber = isOrganization ? recipientWithoutPrefix : null,
                NationalIdentityNumber = isPerson ? recipientWithoutPrefix : null
            });
        }
        
        // Add custom recipients if they exist (in addition to default recipient when OverrideRegisteredContactInformation is false)
        if (notificationRequest.CustomRecipients != null && notificationRequest.CustomRecipients.Any())
        {
            recipientsToProcess.AddRange(notificationRequest.CustomRecipients);
        }

        var notificationOrders = new List<NotificationOrderRequestV2>();

        // Create a notification order for each recipient
        foreach (var recipient in recipientsToProcess)
        {
            var notificationOrder = new NotificationOrderRequestV2
            {
                SendersReference = correspondence.SendersReference,
                RequestedSendTime = correspondence.RequestedPublishTime.UtcDateTime <= DateTime.UtcNow
                    ? DateTime.UtcNow.AddMinutes(5)
                    : correspondence.RequestedPublishTime.UtcDateTime.AddMinutes(5),
                IdempotencyId = correspondence.Id.CreateVersion5(BuildRecipientKey(recipient)),
                Recipient = CreateRecipientOrderV2FromRecipient(recipient, notificationRequest, contents.First(), correspondence, isReminder: false)
            };

            if (notificationRequest.SendReminder)
            {
                notificationOrder.Reminders =
                [
                    new ReminderV2
                    {
                        SendersReference = correspondence.SendersReference,
                        DelayDays = hostEnvironment.IsProduction() ? 7 : 1,
                        ConditionEndpoint = CreateConditionEndpoint(correspondence.Id.ToString())?.ToString(),
                        Recipient = CreateRecipientOrderV2FromRecipient(recipient, notificationRequest, contents.First(), correspondence, isReminder: true)
                    }
                ];
            }
            
            notificationOrders.Add(notificationOrder);
        }
        
        logger.LogInformation("Created {Count} notification request(s) V2 for correspondence {CorrespondenceId}", notificationOrders.Count, correspondence.Id);
        return notificationOrders;
    }

    private static string BuildRecipientKey(Recipient recipient)
    {
        if (!string.IsNullOrEmpty(recipient.OrganizationNumber)) return $"org:{recipient.OrganizationNumber}";
        if (!string.IsNullOrEmpty(recipient.NationalIdentityNumber)) return $"nin:{recipient.NationalIdentityNumber}";
        if (!string.IsNullOrEmpty(recipient.EmailAddress)) return $"email:{recipient.EmailAddress.ToLowerInvariant()}";
        if (!string.IsNullOrEmpty(recipient.MobileNumber)) return $"sms:{recipient.MobileNumber}";
        throw new InvalidOperationException("Recipient must have exactly one identifier");
    }

    private static RecipientV2 CreateRecipientOrderV2FromRecipient(Recipient recipient, NotificationRequest notificationRequest, NotificationContent content, CorrespondenceEntity correspondence, bool isReminder)
    {
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
                Body = emailBody,
                ContentType = isReminder ? notificationRequest.ReminderEmailContentType ?? notificationRequest.EmailContentType : notificationRequest.EmailContentType
            }
            : null;

        var smsSettings = !string.IsNullOrWhiteSpace(smsBody)
            ? new SmsSettings
            {
                Body = smsBody
            }
            : null;

        // Determine recipient type and create appropriate RecipientV2
        if (!string.IsNullOrEmpty(recipient.OrganizationNumber))
        {
            return new RecipientV2
            {
                RecipientOrganization = new RecipientOrganization
                {
                    OrgNumber = recipient.OrganizationNumber,
                    ResourceId = resourceIdWithPrefix,
                    ChannelSchema = channel,
                    EmailSettings = emailSettings,
                    SmsSettings = smsSettings
                }
            };
        }
        else if (!string.IsNullOrEmpty(recipient.NationalIdentityNumber))
        {
            return new RecipientV2
            {
                RecipientPerson = new RecipientPerson
                {
                    NationalIdentityNumber = recipient.NationalIdentityNumber,
                    ResourceId = resourceIdWithPrefix,
                    ChannelSchema = channel,
                    EmailSettings = emailSettings,
                    SmsSettings = smsSettings,
                    IgnoreReservation = correspondence.IgnoreReservation
                }
            };
        }
        else if (!string.IsNullOrEmpty(recipient.EmailAddress))
        {
            return new RecipientV2
            {
                RecipientEmail = new RecipientEmail
                {
                    EmailAddress = recipient.EmailAddress,
                    EmailSettings = emailSettings
                }
            };
        }
        else if (!string.IsNullOrEmpty(recipient.MobileNumber))
        {
            return new RecipientV2
            {
                RecipientSms = new RecipientSms
                {
                    PhoneNumber = recipient.MobileNumber,
                    SmsSettings = smsSettings
                }
            };
        }

        throw new InvalidOperationException("Recipient must have exactly one identifier");
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

    private async Task PersistNotificationOrderRequests(NotificationRequest notificationRequest, CorrespondenceEntity correspondence, List<NotificationContent> notificationContents, CancellationToken cancellationToken)
    {
        // Create notification order requests
        var notificationOrderRequests = CreateNotificationOrderRequestsV2(
            notificationRequest,
            correspondence,
            notificationContents,
            cancellationToken);
        
        logger.LogInformation("Persisting {Count} notification order requests for correspondence {CorrespondenceId}", notificationOrderRequests.Count, correspondence.Id);
        foreach (var notificationOrderRequest in notificationOrderRequests)
        {
            try
            {
                await idempotencyKeyRepository.CreateAsync(new IdempotencyKeyEntity
                {
                    Id = notificationOrderRequest.IdempotencyId,
                    CorrespondenceId = correspondence.Id,
                    IdempotencyType = IdempotencyType.NotificationOrder
                }, cancellationToken);
            }
            catch (DbUpdateException e)
            {
                var sqlState = e.InnerException?.Data["SqlState"]?.ToString();
                if (sqlState == "23505")
                {
                    logger.LogWarning("Primary notification already persisted for idempotency key {IdempotencyId} on correspondence {CorrespondenceId}. Skipping.", notificationOrderRequest.IdempotencyId, correspondence.Id);
                    continue;
                }
                throw;
            }

            var notification = new CorrespondenceNotificationEntity()
            {
                Created = DateTimeOffset.UtcNow,
                NotificationTemplate = notificationRequest.NotificationTemplate,
                NotificationChannel = notificationRequest.NotificationChannel,
                CorrespondenceId = correspondence.Id,
                RequestedSendTime = notificationOrderRequest.RequestedSendTime,
                IsReminder = false,
                OrderRequest = JsonSerializer.Serialize(notificationOrderRequest)
            };
            await correspondenceNotificationRepository.AddNotification(notification, cancellationToken);
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