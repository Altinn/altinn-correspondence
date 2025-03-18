using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using OneOf;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.GetCorrespondenceHistory;
public class LegacyGetCorrespondenceHistoryHandler(
    ICorrespondenceRepository correspondenceRepository,
    IAltinnNotificationService altinnNotificationService,
    IAltinnRegisterService altinnRegisterService,
    IAltinnAuthorizationService altinnAuthorizationService,
    UserClaimsHelper userClaimsHelper) : IHandler<Guid, List<LegacyGetCorrespondenceHistoryResponse>>
{
    public async Task<OneOf<List<LegacyGetCorrespondenceHistoryResponse>, Error>> Process(Guid correspondenceId, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        if (userClaimsHelper.GetPartyId() is not int partyId)
        {
            return AuthorizationErrors.InvalidPartyId;
        }
        var recipientParty = await altinnRegisterService.LookUpPartyByPartyId(partyId, cancellationToken);
        if (recipientParty == null || (string.IsNullOrEmpty(recipientParty.SSN) && string.IsNullOrEmpty(recipientParty.OrgNumber)))
        {
            return AuthorizationErrors.CouldNotFindOrgNo;
        }
        var correspondence = await correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, cancellationToken);
        if (correspondence is null)
        {
            return CorrespondenceErrors.CorrespondenceNotFound;
        }
        var minimumAuthLevel = await altinnAuthorizationService.CheckUserAccessAndGetMinimumAuthLevel(user, recipientParty.SSN, correspondence.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Read }, correspondence.Recipient, cancellationToken);
        if (minimumAuthLevel is null)
        {
            return AuthorizationErrors.LegacyNoAccessToCorrespondence;
        }
        var senderParty = await altinnRegisterService.LookUpPartyById(correspondence.Sender, cancellationToken);
        if (senderParty == null || (string.IsNullOrEmpty(senderParty.SSN) && string.IsNullOrEmpty(senderParty.OrgNumber)))
        {
            return AuthorizationErrors.CouldNotFindOrgNo;
        }
        var correspondenceHistory = new List<LegacyGetCorrespondenceHistoryResponse>();
        foreach (var correspondenceStatus in correspondence.Statuses)
        {
            if (correspondenceStatus.Status.IsAvailableForLegacyRecipient())
            {
                correspondenceHistory.Add(await GetCorrespondenceStatus(correspondenceStatus, recipientParty, senderParty, cancellationToken));
            }
        }

        var notificationHistory = new List<LegacyGetCorrespondenceHistoryResponse>();
        foreach (var notification in correspondence.Notifications)
        {
            if (string.IsNullOrEmpty(notification.NotificationOrderId.ToString())) continue;

            var notificationDetails = await altinnNotificationService.GetNotificationDetails(notification.NotificationOrderId.ToString(), cancellationToken);

            if (notificationDetails?.NotificationsStatusDetails is null) continue;

            var firstEmailDelivery = notificationDetails.NotificationsStatusDetails.Emails?
                .Where(email => email.Succeeded)
                .OrderBy(email => email.SendStatus.LastUpdate)
                .FirstOrDefault();
            var firstSmsDelivery = notificationDetails.NotificationsStatusDetails.Smses?
                .Where(sms => sms.Succeeded)
                .OrderBy(sms => sms.SendStatus.LastUpdate)
                .FirstOrDefault();

            var anySucceededDeliveryRecipient = firstEmailDelivery?.Recipient ?? firstSmsDelivery?.Recipient;
            var anySucceededDeliveryStatus = firstEmailDelivery?.SendStatus ?? firstSmsDelivery?.SendStatus;
            if (anySucceededDeliveryRecipient is not null && anySucceededDeliveryStatus is not null)
            {
                // Meant for the simplified view in Altinn 2 Inbox
                var assemblededRecipient = new Recipient()
                {
                    IsReserved = anySucceededDeliveryRecipient.IsReserved,
                    NationalIdentityNumber = anySucceededDeliveryRecipient.NationalIdentityNumber,
                    OrganizationNumber = anySucceededDeliveryRecipient.OrganizationNumber,
                    EmailAddress = string.Join("; ", notificationDetails.NotificationsStatusDetails.Emails?.Select(email => email.Recipient.EmailAddress) ?? []),
                    MobileNumber = string.Join("; ", notificationDetails.NotificationsStatusDetails.Smses?.Select(sms => sms.Recipient.MobileNumber) ?? [])
                };
                notificationHistory.Add(await GetNotificationStatus(anySucceededDeliveryStatus, assemblededRecipient, notification.IsReminder, cancellationToken));
            }
        }
        List<LegacyGetCorrespondenceHistoryResponse> joinedList = [.. correspondenceHistory.Concat(notificationHistory).OrderByDescending(s => s.StatusChanged)];

        return joinedList;
    }

    private async Task<LegacyGetCorrespondenceHistoryResponse> GetCorrespondenceStatus(CorrespondenceStatusEntity status, Party recipientParty, Party senderParty, CancellationToken cancellationToken)
    {
        List<CorrespondenceStatus> statusBySender =
        [
            CorrespondenceStatus.Published,
        ];        
        
        var party = (Party?)null;
        if (statusBySender.Contains(status.Status))
        {
            party = senderParty;
        }
        else
        {
            party = await altinnRegisterService.LookUpPartyByPartyUuid(status.PartyUuid, cancellationToken);
        }        

        return new LegacyGetCorrespondenceHistoryResponse
        {
            Status = status.Status.ToString(),
            StatusChanged = status.StatusChanged,
            StatusText = $"[Correspondence] {status.StatusText}",
            User = new LegacyUser
            {
                PartyId = party?.PartyId,                
                NationalIdentityNumber = party?.SSN,                
                Name = party?.Name
            }
        };
    }

    private async Task<LegacyGetCorrespondenceHistoryResponse> GetNotificationStatus(StatusExt sendStatus, Recipient recipient, bool isReminder, CancellationToken cancellationToken)
    {
        var response = new LegacyGetCorrespondenceHistoryResponse
        {
            Status = sendStatus.Status,
            StatusChanged = sendStatus.LastUpdate,
            StatusText = $"[{(isReminder ? "Reminder" : "Notification")}] {sendStatus.StatusDescription}",
            User = new LegacyUser(),
            Notification = new LegacyNotification
            {
                EmailAddress = recipient.EmailAddress,
                MobileNumber = recipient.MobileNumber,
                OrganizationNumber = recipient.OrganizationNumber,
                NationalIdentityNumber = recipient.NationalIdentityNumber
            }
        };

        string? id = !string.IsNullOrEmpty(recipient.OrganizationNumber) ? recipient.OrganizationNumber : recipient.NationalIdentityNumber;
        if (!string.IsNullOrEmpty(id))
        {
            var party = await altinnRegisterService.LookUpPartyById(id, cancellationToken);
            response.User = new LegacyUser
            {
                PartyId = party?.PartyId,
                NationalIdentityNumber = recipient.NationalIdentityNumber,
                Name = party?.Name
            };
        }

        return response;
    }
}
