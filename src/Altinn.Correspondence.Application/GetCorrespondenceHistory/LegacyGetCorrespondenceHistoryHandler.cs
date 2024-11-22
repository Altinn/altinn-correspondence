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
    private readonly ICorrespondenceRepository _correspondenceRepository = correspondenceRepository;
    private readonly IAltinnNotificationService _altinnNotificationService = altinnNotificationService;
    private readonly IAltinnRegisterService _altinnRegisterService = altinnRegisterService;
    private readonly IAltinnAuthorizationService _altinnAuthorizationService = altinnAuthorizationService;
    private readonly UserClaimsHelper _userClaimsHelper = userClaimsHelper;

    public async Task<OneOf<List<LegacyGetCorrespondenceHistoryResponse>, Error>> Process(Guid correspondenceId, ClaimsPrincipal? user, CancellationToken cancellationToken)
    {
        if (_userClaimsHelper.GetPartyId() is not int partyId)
        {
            return Errors.InvalidPartyId;
        }
        var recipientParty = await _altinnRegisterService.LookUpPartyByPartyId(partyId, cancellationToken);
        if (recipientParty == null || (string.IsNullOrEmpty(recipientParty.SSN) && string.IsNullOrEmpty(recipientParty.OrgNumber)))
        {
            return Errors.CouldNotFindOrgNo;
        }
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, cancellationToken);
        if (correspondence is null)
        {
            return Errors.CorrespondenceNotFound;
        }
        var minimumAuthLevel = await _altinnAuthorizationService.CheckUserAccessAndGetMinimumAuthLevel(user, recipientParty.SSN, correspondence.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Read }, correspondence.Recipient, cancellationToken);
        if (minimumAuthLevel is null)
        {
            return Errors.LegacyNoAccessToCorrespondence;
        }
        var senderParty = await _altinnRegisterService.LookUpPartyById(correspondence.Sender, cancellationToken);
        if (senderParty == null || (string.IsNullOrEmpty(senderParty.SSN) && string.IsNullOrEmpty(senderParty.OrgNumber)))
        {
            return Errors.CouldNotFindOrgNo;
        }

        var correspondenceHistory = correspondence.Statuses
            .Where(s => s.Status.IsAvailableForRecipient())
            .Select(s => GetCorrespondenceStatus(s, recipientParty, senderParty))
            .ToList();

        var notificationHistory = new List<LegacyGetCorrespondenceHistoryResponse>();
        foreach (var notification in correspondence.Notifications)
        {
            if (string.IsNullOrEmpty(notification.NotificationOrderId.ToString())) continue;

            var notificationDetails = await _altinnNotificationService.GetNotificationDetails(notification.NotificationOrderId.ToString(), cancellationToken);

            if (notificationDetails?.NotificationsStatusDetails is null) continue;

            if (notificationDetails.NotificationsStatusDetails.Sms is not null)
            {
                notificationHistory.Add(GetNotificationStatus(
                    notificationDetails.NotificationsStatusDetails.Sms.SendStatus,
                    notificationDetails.NotificationsStatusDetails.Sms.Recipient,
                    notification.IsReminder,
                    senderParty.PartyId)); // Notification recipient
            }
            if (notificationDetails.NotificationsStatusDetails.Email is not null)
            {
                notificationHistory.Add(GetNotificationStatus(
                    notificationDetails.NotificationsStatusDetails.Email.SendStatus,
                    notificationDetails.NotificationsStatusDetails.Email.Recipient,
                    notification.IsReminder,
                    senderParty.PartyId)); // Notification recipient
            }
        }
        List<LegacyGetCorrespondenceHistoryResponse> joinedList = [.. correspondenceHistory.Concat(notificationHistory).OrderByDescending(s => s.StatusChanged)];

        return joinedList;
    }

    private static LegacyGetCorrespondenceHistoryResponse GetCorrespondenceStatus(CorrespondenceStatusEntity s, Party recipientParty, Party senderParty)
    {
        List<CorrespondenceStatus> statusBySender =
        [
            CorrespondenceStatus.Published,
        ];
        return new LegacyGetCorrespondenceHistoryResponse
        {
            Status = s.Status.ToString(),
            StatusChanged = s.StatusChanged,
            StatusText = $"[Correspondence] {s.StatusText}",
            User = new LegacyUser
            {
                PartyId = statusBySender.Contains(s.Status) ? senderParty.PartyId : recipientParty.PartyId
            }
        };
    }

    private static LegacyGetCorrespondenceHistoryResponse GetNotificationStatus(StatusExt sendStatus, Recipient recipient, bool isReminder, int partyId)
    {
        return new LegacyGetCorrespondenceHistoryResponse
        {
            Status = sendStatus.Status,
            StatusChanged = sendStatus.LastUpdate,
            StatusText = $"[{(isReminder ? "Reminder" : "Notification")}] {sendStatus.StatusDescription}",
            User = new LegacyUser
            {
                PartyId = partyId,
                Recipient = recipient
            },
        };
    }
}
