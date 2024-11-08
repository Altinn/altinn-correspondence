using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using OneOf;

namespace Altinn.Correspondence.Application.GetCorrespondenceHistory;
public class LegacyGetCorrespondenceHistoryHandler : IHandler<Guid, LegacyGetCorrespondenceHistoryResponse>
{
    private readonly ICorrespondenceRepository _correspondenceRepository;
    private readonly IAltinnNotificationService _altinnNotificationService;
    private readonly IAltinnRegisterService _altinnRegisterService;
    private readonly IAltinnAuthorizationService _altinnAuthorizationService;
    private readonly UserClaimsHelper _userClaimsHelper;

    public LegacyGetCorrespondenceHistoryHandler(ICorrespondenceRepository correspondenceRepository, IAltinnNotificationService altinnNotificationService, IAltinnRegisterService altinnRegisterService, IAltinnAuthorizationService altinnAuthorizationService, UserClaimsHelper userClaimsHelper)
    {
        _correspondenceRepository = correspondenceRepository;
        _altinnNotificationService = altinnNotificationService;
        _altinnRegisterService = altinnRegisterService;
        _altinnAuthorizationService = altinnAuthorizationService;
        _userClaimsHelper = userClaimsHelper;
    }
    public async Task<OneOf<LegacyGetCorrespondenceHistoryResponse, Error>> Process(Guid correspondenceId, CancellationToken cancellationToken)
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
        // TODO: Authorize party
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, cancellationToken);
        if (correspondence is null)
        {
            return Errors.CorrespondenceNotFound;
        }
        var senderParty = await _altinnRegisterService.LookUpPartyById(correspondence.Sender, cancellationToken);
        if (senderParty == null || (string.IsNullOrEmpty(senderParty.SSN) && string.IsNullOrEmpty(senderParty.OrgNumber)))
        {
            return Errors.CouldNotFindOrgNo;
        }
        var minimumAuthLevel = await _altinnAuthorizationService.CheckUserAccessAndGetMinimumAuthLevel(recipientParty.SSN, correspondence.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.Read }, correspondence.Recipient, cancellationToken);
        if (minimumAuthLevel is not int authenticationLevel)
        {
            return Errors.LegacyNoAccessToCorrespondence;
        }

        var correspondenceHistory = correspondence.Statuses
            .Where(s => s.Status.IsAvailableForRecipient())
            .Select(s => new LegacyCorrespondenceStatus
            {
                Status = s.Status.ToString(),
                StatusChanged = s.StatusChanged,
                StatusText = $"[Correspondence] {s.StatusText}",
                User = new LegacyUser
                {
                    PartyId = recipientParty.PartyId.ToString(),
                    AuthenticationLevel = authenticationLevel
                },
            }).ToList();

        var notificationHistory = new List<LegacyCorrespondenceStatus>();
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
                    senderParty.PartyId.ToString(),
                    authenticationLevel));
            }
            if (notificationDetails.NotificationsStatusDetails.Email is not null)
            {
                notificationHistory.Add(GetNotificationStatus(
                    notificationDetails.NotificationsStatusDetails.Email.SendStatus,
                    notificationDetails.NotificationsStatusDetails.Email.Recipient,
                    notification.IsReminder,
                    senderParty.PartyId.ToString(),
                    authenticationLevel));
            }
        }

        var legacyHistory = new LegacyGetCorrespondenceHistoryResponse
        {
            History = [.. correspondenceHistory.Concat(notificationHistory).OrderByDescending(s => s.StatusChanged)],
            NeedsConfirm = correspondence.IsConfirmationNeeded,
        };
        return legacyHistory;
    }

    private static LegacyCorrespondenceStatus GetNotificationStatus(StatusExt sendStatus, Recipient recipient, bool isReminder, string partyId, int authenticationLevel)
    {
        return new LegacyCorrespondenceStatus
        {
            Status = sendStatus.Status,
            StatusChanged = sendStatus.LastUpdate,
            StatusText = $"[{(isReminder ? "Reminder" : "Notification")}] {sendStatus.StatusDescription}",
            User = new LegacyUser
            {
                PartyId = partyId,
                AuthenticationLevel = authenticationLevel,
                Recipient = recipient
            },
        };
    }
}
