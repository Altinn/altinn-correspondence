using Altinn.Correspondence.Application.Helpers;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using OneOf;

namespace Altinn.Correspondence.Application.GetCorrespondenceHistory;
public class LegacyGetCorrespondenceHistoryHandler : IHandler<LegacyGetCorrespondenceHistoryRequest, LegacyGetCorrespondenceHistoryResponse>
{
    private readonly ICorrespondenceRepository _correspondenceRepository;
    private readonly IAltinnNotificationService _altinnNotificationService;
    private readonly IAltinnRegisterService _altinnRegisterService;
    public LegacyGetCorrespondenceHistoryHandler(ICorrespondenceRepository correspondenceRepository, IAltinnNotificationService altinnNotificationService, IAltinnRegisterService altinnRegisterService)
    {
        _correspondenceRepository = correspondenceRepository;
        _altinnNotificationService = altinnNotificationService;
        _altinnRegisterService = altinnRegisterService;
    }
    public async Task<OneOf<LegacyGetCorrespondenceHistoryResponse, Error>> Process(LegacyGetCorrespondenceHistoryRequest request, CancellationToken cancellationToken)
    {
        var hasAccess = true; // TODO: Authorize user
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }
        var correspondence = await _correspondenceRepository.GetCorrespondenceById(request.CorrespondenceId, true, true, cancellationToken);
        if (correspondence is null)
        {
            return Errors.CorrespondenceNotFound;
        }

        var recipientParty = await _altinnRegisterService.LookUpPartyById(request.OnBehalfOfPartyId, cancellationToken);
        if (recipientParty == null || (string.IsNullOrEmpty(recipientParty.SSN) && string.IsNullOrEmpty(recipientParty.OrgNumber)))
        {
            return Errors.CouldNotFindOrgNo; // TODO: Update to better error message
        }

        var senderParty = await _altinnRegisterService.LookUpPartyById(correspondence.Sender, cancellationToken);
        if (senderParty == null || (string.IsNullOrEmpty(senderParty.SSN) && string.IsNullOrEmpty(senderParty.OrgNumber)))
        {
            return Errors.CouldNotFindOrgNo; // TODO: Update to better error message
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
                    AuthenticationLevel = 0, // TODO: Get authentication level
                },
            }).ToList();

        var notificationHistory = new List<LegacyCorrespondenceStatus>();
        foreach (var notification in correspondence.Notifications)
        {
            if (notification.NotificationOrderId != null)
            {
                var notificationDetails = await _altinnNotificationService.GetNotificationDetails(notification.NotificationOrderId.ToString());
                if (notificationDetails.NotificationsStatusDetails is null) continue;
                if (notificationDetails.NotificationsStatusDetails.Sms is not null)
                {
                    notificationHistory.Add(new LegacyCorrespondenceStatus
                    {
                        Status = notificationDetails.NotificationsStatusDetails.Sms.SendStatus.Status,
                        StatusChanged = notificationDetails.NotificationsStatusDetails.Sms.SendStatus.LastUpdate,
                        StatusText = $"[{(notification.IsReminder ? "Reminder" : "Notification")}] {notificationDetails.NotificationsStatusDetails.Sms.SendStatus.StatusDescription}",
                        User = new LegacyUser
                        {
                            PartyId = senderParty.PartyId.ToString(),
                            AuthenticationLevel = 0, // TODO: Get authentication level
                        },
                    });
                }
                if (notificationDetails.NotificationsStatusDetails.Email is not null)
                {
                    notificationHistory.Add(new LegacyCorrespondenceStatus
                    {
                        Status = notificationDetails.NotificationsStatusDetails.Email.SendStatus.Status,
                        StatusChanged = notificationDetails.NotificationsStatusDetails.Email.SendStatus.LastUpdate,
                        StatusText = $"[{(notification.IsReminder ? "Reminder" : "Notification")}] {notificationDetails.NotificationsStatusDetails.Email.SendStatus.StatusDescription}",
                        User = new LegacyUser
                        {
                            PartyId = senderParty.PartyId.ToString(),
                            AuthenticationLevel = 0, // TODO: Get authentication level
                        },
                    });
                }
            }
        }

        var legacyHistory = new LegacyGetCorrespondenceHistoryResponse
        {
            History = [.. correspondenceHistory.Concat(notificationHistory).OrderByDescending(s => s.StatusChanged)],
            NeedsConfirm = correspondence.IsConfirmationNeeded,
        };
        return legacyHistory;
    }

}
