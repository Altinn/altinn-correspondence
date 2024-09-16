using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Mappers;

internal static class CorrespondenceNotificationMapper
{

    internal static CorrespondenceNotificationExt MapToExternal(CorrespondenceNotificationEntity correspondenceNotification)
    {
        var notification = new CorrespondenceNotificationExt
        {
            NotificationTemplate = (NotificationTemplateExt)correspondenceNotification.NotificationTemplate,
            NotificationChannel = (NotificationChannelExt)correspondenceNotification.NotificationChannel,
            RequestedSendTime = correspondenceNotification.RequestedSendTime,
            Created = correspondenceNotification.Created,
            NotificationId = correspondenceNotification.Id,
            NotificationOrderId = correspondenceNotification.NotificationOrderId,
        };
        return notification;
    }

    internal static List<CorrespondenceNotificationExt> MapListToExternal(List<CorrespondenceNotificationEntity> notifications)
    {
        var notificationsExt = new List<CorrespondenceNotificationExt>();
        foreach (var not in notifications)
        {
            notificationsExt.Add(MapToExternal(not));
        }
        return notificationsExt;
    }
}
