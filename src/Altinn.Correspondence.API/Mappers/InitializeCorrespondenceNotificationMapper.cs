using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Core.Models;

namespace Altinn.Correspondence.Mappers;

internal static class InitializeCorrespondenceNotificationMapper
{
    internal static CorrespondenceNotificationEntity MapToEntity(InitializeCorrespondenceNotificationExt correspondenceNotificationExt)
    {
        var notification = new CorrespondenceNotificationEntity
        {
            NotificationTemplate = correspondenceNotificationExt.NotificationTemplate,
            RequestedSendTime = correspondenceNotificationExt.RequestedSendTime,
            SendersReference = correspondenceNotificationExt.SendersReference,
            CustomTextToken = correspondenceNotificationExt.CustomTextToken,
            Created = DateTimeOffset.UtcNow
        };
        return notification;
    }

    internal static InitializeCorrespondenceNotificationExt MapToExternal(CorrespondenceNotificationEntity correspondenceNotification)
    {
        var notification = new InitializeCorrespondenceNotificationExt
        {
            NotificationTemplate = correspondenceNotification.NotificationTemplate,
            RequestedSendTime = correspondenceNotification.RequestedSendTime,
            SendersReference = correspondenceNotification.SendersReference,
            CustomTextToken = correspondenceNotification.CustomTextToken
        };
        return notification;
    }

    internal static List<CorrespondenceNotificationEntity> MapListToEntities(List<InitializeCorrespondenceNotificationExt> notificationsExt)
    {
        var notifications = new List<CorrespondenceNotificationEntity>();
        foreach (var not in notificationsExt)
        {
            notifications.Add(MapToEntity(not));
        }
        return notifications;
    }

    internal static List<InitializeCorrespondenceNotificationExt> MapListToExternal(List<CorrespondenceNotificationEntity> notifications)
    {
        var notificationsExt = new List<InitializeCorrespondenceNotificationExt>();
        foreach (var not in notifications)
        {
            notificationsExt.Add(MapToExternal(not));
        }
        return notificationsExt;
    }
}
