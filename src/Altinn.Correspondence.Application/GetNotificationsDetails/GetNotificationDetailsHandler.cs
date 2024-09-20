using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Repositories;
using OneOf;

namespace Altinn.Correspondence.Application.GetNotificationDetails;

public class GetNotificationDetailsHandler : IHandler<Guid, List<NotificationOrderWithStatus>>
{
    private readonly ICorrespondenceRepository _correspondenceRepository;
    private readonly IAltinnAuthorizationService _altinnAuthorizationService;
    private readonly IAltinnNotificationService _altinnNotificationService;

    public GetNotificationDetailsHandler(ICorrespondenceRepository correspondenceRepository, IAltinnAuthorizationService altinnAuthorizationService, IAltinnNotificationService altinnNotificationService)
    {
        _correspondenceRepository = correspondenceRepository;
        _altinnAuthorizationService = altinnAuthorizationService;
        _altinnNotificationService = altinnNotificationService;
    }

    public async Task<OneOf<List<NotificationOrderWithStatus>, Error>> Process(Guid correspondenceId, CancellationToken cancellationToken)
    {

        var correspondence = await _correspondenceRepository.GetCorrespondenceById(correspondenceId, true, true, cancellationToken);
        if (correspondence == null)
        {
            return Errors.CorrespondenceNotFound;
        }
        var hasAccess = await _altinnAuthorizationService.CheckUserAccess(correspondence.ResourceId, new List<ResourceAccessLevel> { ResourceAccessLevel.See }, cancellationToken);
        if (!hasAccess)
        {
            return Errors.NoAccessToResource;
        }
        if (correspondence.Notifications.Count == 0)
        {
            return Errors.CorrespondenceDoesNotHaveNotifications;
        }

        var notificationHistory = new List<NotificationOrderWithStatus>();
        foreach (var notification in correspondence.Notifications)
        {
            var notificationSummary = await _altinnNotificationService.GetNotificationDetails(notification.NotificationOrderId.ToString());
            notificationHistory.Add(notificationSummary);
        }

        return notificationHistory;
    }
}
