using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Core.Models;

namespace Altinn.Correspondence.Mappers;

internal static class CorrespondenceNotificationStatusMapper
{

    internal static NotificationStatusEventExt MapToExternal(CorrespondenceNotificationStatusEntity correspondenceNotification)
    {
        var notification = new NotificationStatusEventExt
        {
            Status = correspondenceNotification.Status,
            StatusText = correspondenceNotification.StatusText,
            StatusChanged = correspondenceNotification.StatusChanged
        };
        return notification;
    }

    internal static List<NotificationStatusEventExt> MapListToExternal(List<CorrespondenceNotificationStatusEntity> notifications)
    {
        var notificationsExt = new List<NotificationStatusEventExt>();
        foreach (var not in notifications)
        {
            notificationsExt.Add(MapToExternal(not));
        }
        return notificationsExt;
    }
}
