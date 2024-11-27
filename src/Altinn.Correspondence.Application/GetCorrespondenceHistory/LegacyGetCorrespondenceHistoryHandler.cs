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

            var notificationDetails = await _altinnNotificationService.GetNotificationDetails(notification.NotificationOrderId.ToString(), cancellationToken);

            if (notificationDetails?.NotificationsStatusDetails is null) continue;

            if (notificationDetails.NotificationsStatusDetails.Sms is not null)
            {
                notificationHistory.Add(await GetNotificationStatus(
                    notificationDetails.NotificationsStatusDetails.Sms.SendStatus,
                    notificationDetails.NotificationsStatusDetails.Sms.Recipient,
                    notification.IsReminder,
                    cancellationToken));
            }
            if (notificationDetails.NotificationsStatusDetails.Email is not null)
            {
                notificationHistory.Add(await GetNotificationStatus(
                    notificationDetails.NotificationsStatusDetails.Email.SendStatus,
                    notificationDetails.NotificationsStatusDetails.Email.Recipient,
                    notification.IsReminder,
                    cancellationToken));
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
        var partyId = statusBySender.Contains(status.Status) ? senderParty.PartyId : recipientParty.PartyId;
        var party = await _altinnRegisterService.LookUpPartyByPartyId(partyId, cancellationToken);

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
            var party = await _altinnRegisterService.LookUpPartyById(id, cancellationToken);
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
