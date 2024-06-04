using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Core.Models;

namespace Altinn.Correspondence.Mappers;

internal static class CorrespondenceNotificationMapper
{

    internal static CorrespondenceNotificationDetailsExt MapToExternal(CorrespondenceNotificationEntity correspondenceNotification)
    {
        var latestStatus = correspondenceNotification.Statuses.OrderByDescending(s => s.StatusChanged).First();
        var notification = new CorrespondenceNotificationDetailsExt
        {
            NotificationTemplate = correspondenceNotification.NotificationTemplate,
            RequestedSendTime = correspondenceNotification.RequestedSendTime,
            SendersReference = correspondenceNotification.SendersReference,
            CustomTextToken = correspondenceNotification.CustomTextToken,
            Created = correspondenceNotification.Created,
            NotificationId = correspondenceNotification.Id,
            StatusHistory = correspondenceNotification.Statuses != null ? CorrespondenceNotificationStatusMapper.MapListToExternal(correspondenceNotification.Statuses) : new List<NotificationStatusEventExt>(),
            Status = latestStatus.Status,
            StatusText = latestStatus.StatusText ?? string.Empty,
            StatusChanged = latestStatus.StatusChanged
        };
        return notification;
    }

    internal static List<CorrespondenceNotificationDetailsExt> MapListToExternal(List<CorrespondenceNotificationEntity> notifications)
    {
        var notificationsExt = new List<CorrespondenceNotificationDetailsExt>();
        foreach (var not in notifications)
        {
            notificationsExt.Add(MapToExternal(not));
        }
        return notificationsExt;
    }
}
