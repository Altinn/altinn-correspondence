using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Application.InitializeCorrespondences;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Common.Helpers.Models;
using Altinn.Correspondence.Core.Extensions;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Models.Profile;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Persistence;
using Altinn.Correspondence.Persistence.Helpers;
using Altinn.Notifications.Core.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Altinn.Correspondence.Application.CreateNotificationOrder;

public class CreateNotificationOrderHandler(
    ICorrespondenceRepository correspondenceRepository,
    IAltinnRegisterService altinnRegisterService,
    IAltinnProfileService altinnProfileService,
    IAltinnAuthorizationService altinnAuthorizationService,
    INotificationTemplateRepository notificationTemplateRepository,
    ICorrespondenceNotificationRepository correspondenceNotificationRepository,
    IIdempotencyKeyRepository idempotencyKeyRepository,
    IResourceRegistryService resourceRegistryService,
    IHostEnvironment hostEnvironment,
    IOptions<GeneralSettings> generalSettings,
    MobileNumberHelper mobileNumberHelper,
    ILogger<CreateNotificationOrderHandler> logger,
    ApplicationDbContext dbContext)
{
    private readonly GeneralSettings _generalSettings = generalSettings.Value;

    public async Task Process(CreateNotificationOrderRequest request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting notification order creation for correspondence {CorrespondenceId}", request.CorrespondenceId);
        var correspondence = await correspondenceRepository.GetCorrespondenceById(request.CorrespondenceId, false, true, false, cancellationToken) ?? throw new Exception($"Correspondence with id {request.CorrespondenceId} not found when creating notification order");
        try
        {
            await ProcessInternal(request.NotificationRequest, NotificationContext.FromCorrespondence(correspondence), request.Language, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create notification order for correspondence {CorrespondenceId}", request.CorrespondenceId);
            throw;
        }
    }

    public async Task Process(CreateNotificationOrderForConfidentialReminders request, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting notification order creation for confidential reminder {ReminderId}", request.Reminder.Id);
        try
        {
            await ProcessInternal(request.NotificationRequest, NotificationContext.FromConfidentialReminder(request.Reminder, request.CorrespondenceId), request.Language, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create notification order for confidential reminder {ReminderId}", request.Reminder.Id);
            throw;
        }
    }

    private async Task ProcessInternal(NotificationRequest notificationRequest, NotificationContext context, string? language, CancellationToken cancellationToken)
    {
        // Get notification templates
        logger.LogInformation("Fetching notification templates for template {NotificationTemplate}", notificationRequest.NotificationTemplate);
        var templates = await notificationTemplateRepository.GetNotificationTemplates(
            notificationRequest.NotificationTemplate,
            cancellationToken,
            language);

        if (templates.Count == 0)
        {
            logger.LogError("No notification templates found for template {NotificationTemplate}", notificationRequest.NotificationTemplate);
            throw new Exception($"No notification templates found for template {notificationRequest.NotificationTemplate}");
        }
        logger.LogInformation("Found {TemplateCount} notification templates", templates.Count);

        // Get notification content
        logger.LogInformation("Getting notification content for {NotificationId}", context.Id);
        var notificationContents = await GetNotificationContent(notificationRequest, templates, context, cancellationToken, language);

        // Persist notification order requests
        await PersistNotificationOrderRequests(notificationRequest, context, notificationContents, cancellationToken);
    }

    private async Task<List<NotificationContent>> GetNotificationContent(NotificationRequest request, List<NotificationTemplateEntity> templates, NotificationContext context, CancellationToken cancellationToken, string? language = null)
    {
        var content = new List<NotificationContent>();
        var sendersName = context.MessageSender;
        if (string.IsNullOrEmpty(sendersName))
        {
            logger.LogInformation("Looking up sender name for {NotificationId}", context.Id);
            sendersName = (await altinnRegisterService.LookUpPartyById(context.SenderUrn!, cancellationToken))?.GetDisplayName();
        }
        logger.LogInformation("Looking up recipient name for {NotificationId}", context.Id);
        var recipientName = (await altinnRegisterService.LookUpPartyById(context.Recipient, cancellationToken))?.GetDisplayName();
        var messageTitle = context.MessageTitle ?? string.Empty;

        foreach (var template in templates)
        {
            logger.LogInformation("Processing template {TemplateId} with language {Language}", template.Id, template.Language);
            var resourceName = await resourceRegistryService.GetResourceTitle(context.ResourceId, template.Language ?? language, cancellationToken) ?? context.ResourceId;

            content.Add(new NotificationContent()
            {
                EmailSubject = CreateNotificationContentFromToken(template.EmailSubject ?? string.Empty, request.EmailSubject)
                    .Replace("$sendersName$", sendersName)
                    .Replace("$correspondenceRecipientName$", recipientName)
                    .Replace("$resourceName$", resourceName)
                    .Replace("$messageTitle$", messageTitle),
                EmailBody = CreateNotificationContentFromToken(template.EmailBody ?? string.Empty, request.EmailBody)
                    .Replace("$sendersName$", sendersName)
                    .Replace("$correspondenceRecipientName$", recipientName)
                    .Replace("$resourceName$", resourceName)
                    .Replace("$messageTitle$", messageTitle),
                SmsBody = CreateNotificationContentFromToken(template.SmsBody ?? string.Empty, request.SmsBody)
                    .Replace("$sendersName$", sendersName)
                    .Replace("$correspondenceRecipientName$", recipientName)
                    .Replace("$resourceName$", resourceName)
                    .Replace("$messageTitle$", messageTitle),
                ReminderEmailBody = CreateNotificationContentFromToken(template.ReminderEmailBody ?? string.Empty, request.ReminderEmailBody)
                    .Replace("$sendersName$", sendersName)
                    .Replace("$correspondenceRecipientName$", recipientName)
                    .Replace("$resourceName$", resourceName)
                    .Replace("$messageTitle$", messageTitle),
                ReminderEmailSubject = CreateNotificationContentFromToken(template.ReminderEmailSubject ?? string.Empty, request.ReminderEmailSubject)
                    .Replace("$sendersName$", sendersName)
                    .Replace("$correspondenceRecipientName$", recipientName)
                    .Replace("$resourceName$", resourceName)
                    .Replace("$messageTitle$", messageTitle),
                ReminderSmsBody = CreateNotificationContentFromToken(template.ReminderSmsBody ?? string.Empty, request.ReminderSmsBody)
                    .Replace("$sendersName$", sendersName)
                    .Replace("$correspondenceRecipientName$", recipientName)
                    .Replace("$resourceName$", resourceName)
                    .Replace("$messageTitle$", messageTitle),
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

    private async Task<List<NotificationOrderRequestV2>> CreateNotificationOrderRequestsV2(NotificationRequest notificationRequest, NotificationContext context, List<NotificationContent> contents, CancellationToken cancellationToken)
    {
        logger.LogInformation("Creating notification order request V2 for {NotificationId}", context.Id);

        // Determine recipients to process - behavior depends on OverrideRegisteredContactInformation flag
        List<Recipient> recipientsToProcess = new List<Recipient>();
        Recipient? correspondenceRecipient = null;

        // If OverrideRegisteredContactInformation is false (default), add the default correspondence recipient
        if (!notificationRequest.OverrideRegisteredContactInformation)
        {
            string recipient = context.Recipient;
            string recipientWithoutPrefix = recipient.WithoutPrefix();
            bool isExternalIdentity =
                recipient.StartsWith($"{UrnConstants.PersonIdPortenEmailAttribute}:", StringComparison.Ordinal)
                || recipient.StartsWith($"{UrnConstants.PersonLegacySelfIdentifiedAttribute}:", StringComparison.Ordinal);
            bool isOrganization = recipientWithoutPrefix.IsOrganizationNumber();
            bool isPerson = recipientWithoutPrefix.IsSocialSecurityNumberWithNoPrefix();

            if (!isExternalIdentity && !isOrganization && !isPerson)
            {
                throw new InvalidOperationException($"Unsupported correspondence recipient format for notifications: {recipient}");
            }

            correspondenceRecipient = new Recipient
            {
                OrganizationNumber = isOrganization ? recipientWithoutPrefix : null,
                NationalIdentityNumber = isPerson ? recipientWithoutPrefix : null,
                ExternalIdentity = isExternalIdentity ? recipient : null
            };
            recipientsToProcess.Add(correspondenceRecipient);

        }

        // Add custom recipients if they exist (in addition to default recipient when OverrideRegisteredContactInformation is false)
        if (notificationRequest.CustomRecipients != null && notificationRequest.CustomRecipients.Any())
        {
            recipientsToProcess.AddRange(notificationRequest.CustomRecipients);
        }

        if (correspondenceRecipient is not null)
        {
            recipientsToProcess = await RemoveCustomRecipientsAlreadyOnRecipient(recipientsToProcess, correspondenceRecipient, notificationRequest, context, cancellationToken);
        }

        // Deduplicate recipients based on the same key used for idempotency to avoid tracking and PK conflicts
        var distinctRecipients = recipientsToProcess
            .GroupBy(BuildRecipientKey)
            .Select(g => g.First())
            .ToList();

        if (distinctRecipients.Count != recipientsToProcess.Count)
        {
            logger.LogInformation(
                "Deduplicated recipients for {NotificationId}: {OriginalCount} -> {DistinctCount}",
                context.Id,
                recipientsToProcess.Count,
                distinctRecipients.Count);
        }

        var notificationOrders = new List<NotificationOrderRequestV2>();

        foreach (var recipient in distinctRecipients)
        {
            var notificationOrder = new NotificationOrderRequestV2
            {
                SendersReference = $"corr-{context.SendersReference}",
                RequestedSendTime = context.RequestedPublishTime.UtcDateTime <= DateTime.UtcNow
                    ? DateTime.UtcNow
                    : context.RequestedPublishTime.UtcDateTime,
                IdempotencyId = context.Id.CreateVersion5(BuildRecipientKey(recipient)),
                Recipient = CreateRecipientOrderV2FromRecipient(recipient, notificationRequest, contents.First(), context, isReminder: false)
            };

            if (notificationRequest.SendReminder)
            {
                notificationOrder.Reminders =
                [
                    new ReminderV2
                    {
                        SendersReference = $"corr-{context.SendersReference}",
                        DelayDays = hostEnvironment.IsProduction() ? 7 : 1,
                        ConditionEndpoint = CreateConditionEndpoint(context.CorrespondenceId.ToString())?.ToString(),
                        Recipient = CreateRecipientOrderV2FromRecipient(recipient, notificationRequest, contents.First(), context, isReminder: true)
                    }
                ];
            }

            notificationOrders.Add(notificationOrder);
        }

        logger.LogInformation("Created {Count} notification request(s) V2 for {NotificationId}", notificationOrders.Count, context.Id);
        return notificationOrders;
    }

    /// <summary>
    /// Removes custom email/SMS recipients whose address is already registered on the correspondence recipient,
    /// since those addresses receive the notification sent to the recipient's registered contact information.
    /// </summary>
    private async Task<List<Recipient>> RemoveCustomRecipientsAlreadyOnRecipient(List<Recipient> recipients, Recipient correspondenceRecipient, NotificationRequest notificationRequest, NotificationContext context, CancellationToken cancellationToken)
    {
        var channels = new List<NotificationChannel> { notificationRequest.NotificationChannel };
        if (notificationRequest.SendReminder)
        {
            channels.Add(notificationRequest.ReminderNotificationChannel ?? notificationRequest.NotificationChannel);
        }
        bool notifiesRegisteredEmails = channels.Any(channel => channel is NotificationChannel.Email or NotificationChannel.EmailAndSms or NotificationChannel.EmailPreferred);
        bool notifiesRegisteredMobileNumbers = channels.Any(channel => channel is NotificationChannel.Sms or NotificationChannel.EmailAndSms or NotificationChannel.SmsPreferred);

        var customEmails = notifiesRegisteredEmails
            ? recipients.Where(recipient => !string.IsNullOrEmpty(recipient.EmailAddress)).Select(recipient => recipient.EmailAddress!.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var customMobileNumbers = notifiesRegisteredMobileNumbers
            ? recipients.Where(recipient => !string.IsNullOrEmpty(recipient.MobileNumber)).Select(recipient => NormalizeMobileNumber(recipient.MobileNumber!)).ToHashSet()
            : new HashSet<string>();

        if (customEmails.Count == 0 && customMobileNumbers.Count == 0)
        {
            return recipients;
        }

        var registeredAddresses = correspondenceRecipient switch
        {
            { OrganizationNumber: { } organizationNumber } => await GetAddressesRegisteredOnOrganization(organizationNumber, context.ResourceId, customEmails, customMobileNumbers, cancellationToken),
            // TODO: Altinn Profile has no contact information lookup for persons or self-identified users exposed to correspondence yet
            _ => RegisteredAddresses.None
        };

        var registeredEmails = notifiesRegisteredEmails
            ? registeredAddresses.Emails.Select(email => email.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var registeredMobileNumbers = notifiesRegisteredMobileNumbers
            ? registeredAddresses.MobileNumbers.Select(NormalizeMobileNumber).ToHashSet()
            : [];

        if (registeredEmails.Count == 0 && registeredMobileNumbers.Count == 0)
        {
            return recipients;
        }

        bool IsRegisteredOnRecipient(Recipient recipient) =>
            (!string.IsNullOrEmpty(recipient.EmailAddress) && registeredEmails.Contains(recipient.EmailAddress.Trim()))
            || (!string.IsNullOrEmpty(recipient.MobileNumber) && registeredMobileNumbers.Contains(NormalizeMobileNumber(recipient.MobileNumber)));

        var filteredRecipients = recipients.Where(recipient => !IsRegisteredOnRecipient(recipient)).ToList();

        if (filteredRecipients.Count != recipients.Count)
        {
            logger.LogInformation(
                "Removed {RemovedCount} custom recipient(s) for {NotificationId} because their address is already registered on the correspondence recipient",
                recipients.Count - filteredRecipients.Count,
                context.Id);
        }

        return filteredRecipients;
    }

    private async Task<RegisteredAddresses> GetAddressesRegisteredOnOrganization(string organizationNumber, string resourceId, HashSet<string> customEmails, HashSet<string> customMobileNumbers, CancellationToken cancellationToken)
    {
        var organizationNumbers = new List<string> { organizationNumber };
        var organizationAddresses = await altinnProfileService.GetOrganizationNotificationAddresses(organizationNumbers, cancellationToken);
        var userRegisteredContactPoints = await altinnProfileService.GetUserRegisteredContactPoints(organizationNumbers, resourceId, cancellationToken);
        var authorizedUserContactPoints = await GetAuthorizedUserContactPointsMatchingCustomRecipients(userRegisteredContactPoints, resourceId, customEmails, customMobileNumbers, cancellationToken);

        var emails = organizationAddresses.SelectMany(organization => organization.EmailList)
            .Concat(authorizedUserContactPoints.Select(contactPoint => contactPoint.Email))
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Select(email => email!)
            .ToList();

        var mobileNumbers = organizationAddresses.SelectMany(organization => organization.MobileNumberList)
            .Concat(authorizedUserContactPoints.Select(contactPoint => contactPoint.MobileNumber))
            .Where(mobileNumber => !string.IsNullOrWhiteSpace(mobileNumber))
            .Select(mobileNumber => mobileNumber!)
            .ToList();

        return new RegisteredAddresses(emails, mobileNumbers);
    }

    /// <summary>
    /// Returns the user-registered contact points whose address matches a custom recipient and whose user is authorized
    /// for the resource. Notifications performs the same authorization before sending to a user's registered contact
    /// information, so an unauthorized user's address is never actually delivered to and must not deduplicate a custom
    /// recipient. Only user contact points matching a custom recipient are authorized, since the rest cannot affect the result.
    /// </summary>
    private async Task<List<UserRegisteredContactPoint>> GetAuthorizedUserContactPointsMatchingCustomRecipients(List<UnitContactPoints> unitContactPoints, string resourceId, HashSet<string> customEmails, HashSet<string> customMobileNumbers, CancellationToken cancellationToken)
    {
        bool MatchesCustomRecipient(UserRegisteredContactPoint contactPoint) =>
            (!string.IsNullOrWhiteSpace(contactPoint.Email) && customEmails.Contains(contactPoint.Email.Trim()))
            || (!string.IsNullOrWhiteSpace(contactPoint.MobileNumber) && customMobileNumbers.Contains(NormalizeMobileNumber(contactPoint.MobileNumber)));

        var authorizedContactPoints = new List<UserRegisteredContactPoint>();
        foreach (var unit in unitContactPoints)
        {
            var candidates = unit.UserContactPoints.Where(MatchesCustomRecipient).ToList();
            if (candidates.Count == 0)
            {
                continue;
            }
            var userIds = candidates.Select(contactPoint => contactPoint.UserId).Distinct().ToList();
            var authorizedUserIds = (await altinnAuthorizationService.AuthorizeUserIdsForResource(unit.PartyId, userIds, resourceId, cancellationToken)).ToHashSet();
            authorizedContactPoints.AddRange(candidates.Where(contactPoint => authorizedUserIds.Contains(contactPoint.UserId)));
        }
        return authorizedContactPoints;
    }

    private string NormalizeMobileNumber(string mobileNumber)
    {
        return mobileNumberHelper.EnsureCountryCodeIfValidNumber(mobileNumber.Replace(" ", string.Empty));
    }

    private sealed record RegisteredAddresses(List<string> Emails, List<string> MobileNumbers)
    {
        public static readonly RegisteredAddresses None = new([], []);
    }

    private static string BuildRecipientKey(Recipient recipient)
    {
        if (!string.IsNullOrEmpty(recipient.OrganizationNumber)) return $"org:{recipient.OrganizationNumber}";
        if (!string.IsNullOrEmpty(recipient.NationalIdentityNumber)) return $"nin:{recipient.NationalIdentityNumber}";
        if (!string.IsNullOrEmpty(recipient.ExternalIdentity)) return $"ext:{recipient.ExternalIdentity.ToLowerInvariant()}";
        if (!string.IsNullOrEmpty(recipient.EmailAddress)) return $"email:{recipient.EmailAddress.ToLowerInvariant()}";
        if (!string.IsNullOrEmpty(recipient.MobileNumber)) return $"sms:{recipient.MobileNumber}";
        throw new InvalidOperationException("Recipient must have exactly one identifier");
    }

    private static RecipientV2 CreateRecipientOrderV2FromRecipient(Recipient recipient, NotificationRequest notificationRequest, NotificationContent content, NotificationContext context, bool isReminder)
    {
        var resourceIdWithPrefix = UrnConstants.Resource + ":" + context.ResourceId;
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
        else if (!string.IsNullOrEmpty(recipient.ExternalIdentity))
        {
            return new RecipientV2
            {
                RecipientExternalIdentity = new RecipientExternalIdentity
                {
                    ExternalIdentity = recipient.ExternalIdentity,
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
                    IgnoreReservation = context.IgnoreReservation
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

    private async Task PersistNotificationOrderRequests(NotificationRequest notificationRequest, NotificationContext context, List<NotificationContent> notificationContents, CancellationToken cancellationToken)
    {
        // Create notification order requests
        var notificationOrderRequests = await CreateNotificationOrderRequestsV2(notificationRequest, context, notificationContents, cancellationToken);

        logger.LogInformation("Persisting {Count} notification order requests for {NotificationId}", notificationOrderRequests.Count, context.Id);
        foreach (var notificationOrderRequest in notificationOrderRequests)
        {
            await DatabaseTransactionHelper.ExecuteAsync(dbContext, async (ct) =>
            {
                try
                {
                    await idempotencyKeyRepository.CreateAsync(new IdempotencyKeyEntity
                    {
                        Id = notificationOrderRequest.IdempotencyId,
                        CorrespondenceId = context.CorrespondenceId,
                        IdempotencyType = IdempotencyType.NotificationOrder
                    }, ct);
                }
                catch (DbUpdateException e)
                {
                    if (e.IsPostgresUniqueViolation())
                    {
                        logger.LogWarning("Primary notification already persisted for idempotency key {IdempotencyId} on {NotificationId}. Skipping.", notificationOrderRequest.IdempotencyId, context.Id);
                        return Task.CompletedTask;
                    }
                    throw;
                }

                var notification = new CorrespondenceNotificationEntity()
                {
                    Created = DateTimeOffset.UtcNow,
                    NotificationTemplate = notificationRequest.NotificationTemplate,
                    NotificationChannel = notificationRequest.NotificationChannel,
                    CorrespondenceId = context.CorrespondenceId,
                    RequestedSendTime = notificationOrderRequest.RequestedSendTime,
                    IsReminder = false,
                    OrderRequest = JsonSerializer.Serialize(notificationOrderRequest)
                };
                await correspondenceNotificationRepository.AddNotification(notification, ct);
                return Task.CompletedTask;
            }, cancellationToken);
        }
    }

    internal record NotificationContext
    {
        public required Guid Id { get; init; }

        public required Guid CorrespondenceId { get; init; }

        public required string Recipient { get; init; }
        public required string ResourceId { get; init; }
        public required string SendersReference { get; init; }

        public string? MessageSender { get; init; }

        public string? SenderUrn { get; init; }

        public string? MessageTitle { get; init; }
        public DateTimeOffset RequestedPublishTime { get; init; }
        public bool? IgnoreReservation { get; init; }

        public static NotificationContext FromCorrespondence(CorrespondenceEntity correspondence) => new()
        {
            Id = correspondence.Id,
            CorrespondenceId = correspondence.Id,
            Recipient = correspondence.Recipient,
            ResourceId = correspondence.ResourceId,
            SendersReference = correspondence.SendersReference,
            MessageSender = string.IsNullOrEmpty(correspondence.MessageSender) ? null : correspondence.MessageSender,
            SenderUrn = correspondence.Sender,
            MessageTitle = correspondence.Content?.MessageTitle,
            RequestedPublishTime = correspondence.RequestedPublishTime,
            IgnoreReservation = correspondence.IgnoreReservation
        };

        public static NotificationContext FromConfidentialReminder(ConfidentialReminderDialogDto reminder, Guid correspondenceId) => new()
        {
            Id = reminder.Id,
            CorrespondenceId = correspondenceId,
            Recipient = reminder.Recipient,
            ResourceId = reminder.ResourceId,
            SendersReference = reminder.SendersReference,
            SenderUrn = reminder.Sender,
            MessageTitle = reminder.Title,
            RequestedPublishTime = DateTimeOffset.UtcNow,
            IgnoreReservation = null
        };
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