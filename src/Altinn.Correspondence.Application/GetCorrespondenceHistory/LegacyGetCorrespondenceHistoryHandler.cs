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
    NotificationMapper notificationMapper,
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
        var correspondence = await correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, true, cancellationToken, true);
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
                correspondenceHistory.Add(await GetCorrespondenceStatus(correspondenceStatus, recipientParty, senderParty, correspondence.MessageSender, cancellationToken));
            }
        }

        var notificationHistory = new List<LegacyGetCorrespondenceHistoryResponse>();
        foreach (var notification in correspondence.Notifications)
        {
            if (notification.ShipmentId == null && notification.NotificationOrderId == null && !notification.Altinn2NotificationId.HasValue) continue;

            NotificationStatusResponse? notificationDetails;
            // If the notification does not have a shipmentId, it is a version 1 notification
            if (notification.ShipmentId == null && notification.NotificationOrderId != null)
            {
                notificationDetails = await altinnNotificationService.GetNotificationDetails(notification.NotificationOrderId.ToString(), cancellationToken);
            }
            // If the notification has a shipmentId, it is a version 2 notification
            else if (notification.ShipmentId is not null)
            {
                var notificationDetailsV2 = await altinnNotificationService.GetNotificationDetailsV2(notification.ShipmentId.ToString(), cancellationToken);
                notificationDetails = await notificationMapper.MapNotificationV2ToV1Async(notificationDetailsV2, notification);
            }
            else if (notification.Altinn2NotificationId.HasValue)
            {
                notificationHistory.Add(GetAltinn2NotificationStatus(notification));
                continue;
            }
            else
            {
                continue;
            }

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

        var forwardingEventHistory = new List<LegacyGetCorrespondenceHistoryResponse>();
        foreach (var forwardingEvent in correspondence.ForwardingEvents)
        {
            forwardingEventHistory.Add(await GetForwardingEvent(forwardingEvent, cancellationToken));
        }

        List<LegacyGetCorrespondenceHistoryResponse> joinedList = [.. correspondenceHistory.Concat(notificationHistory).Concat(forwardingEventHistory).OrderByDescending(s => s.StatusChanged)];

        return joinedList;
    }

    private async Task<LegacyGetCorrespondenceHistoryResponse> GetCorrespondenceStatus(CorrespondenceStatusEntity status, Party recipientParty, Party senderParty, string? messageSender, CancellationToken cancellationToken)
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
                Name = messageSender ?? party?.Name
            }
        };
    }

    private LegacyGetCorrespondenceHistoryResponse GetAltinn2NotificationStatus(CorrespondenceNotificationEntity notification)
    {
        return new LegacyGetCorrespondenceHistoryResponse()
        {
            Notification = new LegacyNotification()
            {
                EmailAddress = notification.NotificationChannel == NotificationChannel.Email ? notification.NotificationAddress : null,
                MobileNumber = notification.NotificationChannel == NotificationChannel.Sms ? notification.NotificationAddress : null,
                NationalIdentityNumber = null,
                OrganizationNumber = null
            },
            Status = "Completed",
            StatusText = "Completed",
            User = new LegacyUser(),
            StatusChanged = notification.NotificationSent
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
                Name = party?.Name
            };
        }

        return response;
    }

    private async Task<LegacyGetCorrespondenceHistoryResponse> GetForwardingEvent(CorrespondenceForwardingEventEntity forwardingEventEntity, CancellationToken cancellationToken)
    {
        var response = new LegacyGetCorrespondenceHistoryResponse
        {
            Status = "ElementForwarded",
            StatusChanged = forwardingEventEntity.ForwardedOnDate,
            StatusText = "[Correspondence] Forwarded",
            User =  new LegacyUser(),
            ForwardingEvent = new LegacyForwardingEvent
            {
                ForwardedByPartyUuid = forwardingEventEntity.ForwardedByPartyUuid,
                ForwardedByUserId = forwardingEventEntity.ForwardedByUserId,
                ForwardedByUserUuid = forwardingEventEntity.ForwardedByUserUuid,
                ForwardedToUserId = forwardingEventEntity.ForwardedToUserId,
                ForwardedToUserUuid = forwardingEventEntity.ForwardedToUserUuid,
                ForwardedToEmail = forwardingEventEntity.ForwardedToEmailAddress,
                ForwardingText = forwardingEventEntity.ForwardingText,
                MailboxSupplier = forwardingEventEntity.MailboxSupplier
            }
        };
        var party = await altinnRegisterService.LookUpPartyByPartyUuid(forwardingEventEntity.ForwardedByPartyUuid, cancellationToken);
        response.User = new LegacyUser
        {
            PartyId = party?.PartyId,
            Name = party?.Name
        };

        return response;
    }
}
