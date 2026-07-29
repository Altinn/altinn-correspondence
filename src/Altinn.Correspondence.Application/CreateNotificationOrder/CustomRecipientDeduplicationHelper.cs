using Altinn.Correspondence.Application.InitializeCorrespondences;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Models.Profile;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Notifications.Core.Helpers;
using Microsoft.Extensions.Logging;

namespace Altinn.Correspondence.Application.CreateNotificationOrder;

public class CustomRecipientDeduplicationHelper(
    IAltinnProfileService altinnProfileService,
    IAltinnAuthorizationService altinnAuthorizationService,
    MobileNumberHelper mobileNumberHelper,
    ILogger<CustomRecipientDeduplicationHelper> logger)
{
    /// <summary>
    /// Builds the notification plan for each recipient (whether to send the main notification and/or the reminder),
    /// deduplicating custom recipients against the contact information the correspondence recipient's own notification
    /// already delivers to. Deduplication is evaluated per phase (main notification vs reminder), since the two can use
    /// different channels. What the correspondence recipient's notification actually delivers to is resolved per
    /// recipient type; only organization recipients are supported today, so custom recipients are never deduplicated
    /// against a person or self-identified correspondence recipient (nor when its registered contact information is
    /// overridden). This method holds the logic common to all recipient types and dispatches the type-specific lookup.
    /// </summary>
    public async Task<List<RecipientNotificationPlan>> Deduplicate(
        List<Recipient> recipients,
        Recipient? correspondenceRecipient,
        NotificationRequest notificationRequest,
        string resourceId,
        Guid notificationId,
        CancellationToken cancellationToken)
    {
        static bool NotifiesEmail(NotificationChannel channel) => channel is NotificationChannel.Email or NotificationChannel.EmailAndSms or NotificationChannel.EmailPreferred;
        static bool NotifiesMobileNumber(NotificationChannel channel) => channel is NotificationChannel.Sms or NotificationChannel.EmailAndSms or NotificationChannel.SmsPreferred;

        var reminderChannel = notificationRequest.ReminderNotificationChannel ?? notificationRequest.NotificationChannel;
        bool mainNotifiesEmail = NotifiesEmail(notificationRequest.NotificationChannel);
        bool mainNotifiesMobileNumber = NotifiesMobileNumber(notificationRequest.NotificationChannel);
        bool reminderNotifiesEmail = notificationRequest.SendReminder && NotifiesEmail(reminderChannel);
        bool reminderNotifiesMobileNumber = notificationRequest.SendReminder && NotifiesMobileNumber(reminderChannel);

        var keepAll = recipients.Select(recipient => new RecipientNotificationPlan(recipient, IncludeMain: true, IncludeReminder: notificationRequest.SendReminder)).ToList();
        if (correspondenceRecipient is null)
        {
            return keepAll;
        }

        var customEmails = mainNotifiesEmail || reminderNotifiesEmail
            ? recipients.Where(recipient => !string.IsNullOrEmpty(recipient.EmailAddress)).Select(recipient => recipient.EmailAddress!.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var customMobileNumbers = mainNotifiesMobileNumber || reminderNotifiesMobileNumber
            ? recipients.Where(recipient => !string.IsNullOrEmpty(recipient.MobileNumber)).Select(recipient => NormalizeMobileNumber(recipient.MobileNumber!)).ToHashSet()
            : new HashSet<string>();

        if (customEmails.Count == 0 && customMobileNumbers.Count == 0)
        {
            return keepAll;
        }

        var registeredContactInfo = correspondenceRecipient switch
        {
            { OrganizationNumber: { } organizationNumber } => await GetOrganizationRegisteredContactInfo(organizationNumber, notificationRequest, resourceId, customEmails, customMobileNumbers, notificationId, cancellationToken),
            // TODO: deduplication for person and self-identified correspondence recipients is not implemented yet
            _ => null
        };
        if (registeredContactInfo is null)
        {
            return keepAll;
        }

        bool RegisteredInPhase(Recipient recipient, bool notifiesEmail, bool notifiesMobileNumber) =>
            (notifiesEmail && !string.IsNullOrEmpty(recipient.EmailAddress) && registeredContactInfo.Emails.Contains(recipient.EmailAddress.Trim()))
            || (notifiesMobileNumber && !string.IsNullOrEmpty(recipient.MobileNumber) && registeredContactInfo.MobileNumbers.Contains(NormalizeMobileNumber(recipient.MobileNumber)));

        var plans = new List<RecipientNotificationPlan>();
        int removedRecipients = 0;
        int suppressedReminders = 0;
        int reminderOnlyOrders = 0;
        foreach (var recipient in recipients)
        {
            bool includeMain = !RegisteredInPhase(recipient, mainNotifiesEmail, mainNotifiesMobileNumber);
            bool includeReminder = notificationRequest.SendReminder && !RegisteredInPhase(recipient, reminderNotifiesEmail, reminderNotifiesMobileNumber);

            if (!includeMain && !includeReminder)
            {
                removedRecipients++;
                continue;
            }
            if (!includeMain)
            {
                reminderOnlyOrders++;
            }
            else if (notificationRequest.SendReminder && !includeReminder)
            {
                suppressedReminders++;
            }
            plans.Add(new RecipientNotificationPlan(recipient, includeMain, includeReminder));
        }

        if (removedRecipients > 0 || suppressedReminders > 0 || reminderOnlyOrders > 0)
        {
            logger.LogInformation(
                "Deduplicated custom recipients for {NotificationId} against registered contact information: removed {RemovedRecipients} recipient(s), suppressed {SuppressedReminders} reminder(s), emitted {ReminderOnlyOrders} reminder-only order(s) where the main notification duplicated",
                notificationId,
                removedRecipients,
                suppressedReminders,
                reminderOnlyOrders);
        }

        return plans;
    }

    /// <summary>
    /// Resolves the contact information an organization correspondence recipient's own notification delivers to, or null
    /// when custom recipients must not be deduplicated against it. The organization's order bundles the main notification
    /// and the reminder, and Altinn Notifications rejects the whole order if the organization lacks contact information for
    /// a channel it uses; only the official organization addresses are resolved when the order is created, so if they do not
    /// cover every channel the order delivers nothing and null is returned. Otherwise the order delivers to the official
    /// addresses plus the user-registered contact points authorized for the resource, which is the returned set.
    /// </summary>
    private async Task<RegisteredContactInfo?> GetOrganizationRegisteredContactInfo(string organizationNumber, NotificationRequest notificationRequest, string resourceId, HashSet<string> customEmails, HashSet<string> customMobileNumbers, Guid notificationId, CancellationToken cancellationToken)
    {
        var organizationNumbers = new List<string> { organizationNumber };
        var organizationAddresses = await altinnProfileService.GetOrganizationNotificationAddresses(organizationNumbers, cancellationToken);
        var officialEmails = organizationAddresses.SelectMany(organization => organization.EmailList)
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Select(email => email.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var officialMobileNumbers = organizationAddresses.SelectMany(organization => organization.MobileNumberList)
            .Where(mobileNumber => !string.IsNullOrWhiteSpace(mobileNumber))
            .Select(NormalizeMobileNumber)
            .ToHashSet();

        bool OrganizationCanReceive(NotificationChannel channel) => channel switch
        {
            NotificationChannel.Email => officialEmails.Count > 0,
            NotificationChannel.Sms => officialMobileNumbers.Count > 0,
            NotificationChannel.EmailAndSms => officialEmails.Count > 0 && officialMobileNumbers.Count > 0,
            NotificationChannel.EmailPreferred or NotificationChannel.SmsPreferred => officialEmails.Count > 0 || officialMobileNumbers.Count > 0,
            _ => false
        };

        var reminderChannel = notificationRequest.ReminderNotificationChannel ?? notificationRequest.NotificationChannel;
        bool organizationOrderWillDeliver = OrganizationCanReceive(notificationRequest.NotificationChannel)
            && (!notificationRequest.SendReminder || OrganizationCanReceive(reminderChannel));
        if (!organizationOrderWillDeliver)
        {
            logger.LogInformation("Skipping custom recipient deduplication for {NotificationId}: the organization is missing contact information for a channel the notification order uses, so the order would not deliver", notificationId);
            return null;
        }

        var userRegisteredContactPoints = await altinnProfileService.GetUserRegisteredContactPoints(organizationNumbers, resourceId, cancellationToken);
        var authorizedUserContactPoints = await GetAuthorizedUserContactPointsMatchingCustomRecipients(userRegisteredContactPoints, resourceId, customEmails, customMobileNumbers, cancellationToken);

        var registeredEmails = officialEmails
            .Concat(authorizedUserContactPoints.Where(contactPoint => !string.IsNullOrWhiteSpace(contactPoint.Email)).Select(contactPoint => contactPoint.Email!.Trim()))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var registeredMobileNumbers = officialMobileNumbers
            .Concat(authorizedUserContactPoints.Where(contactPoint => !string.IsNullOrWhiteSpace(contactPoint.MobileNumber)).Select(contactPoint => NormalizeMobileNumber(contactPoint.MobileNumber!)))
            .ToHashSet();

        return new RegisteredContactInfo(registeredEmails, registeredMobileNumbers);
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

    private sealed record RegisteredContactInfo(HashSet<string> Emails, HashSet<string> MobileNumbers);

    /// <summary>
    /// The plan for a single recipient: whether the main notification and/or its reminder should be sent, after
    /// deduplicating against the correspondence recipient's registered contact information.
    /// </summary>
    public sealed record RecipientNotificationPlan(Recipient Recipient, bool IncludeMain, bool IncludeReminder);
}
