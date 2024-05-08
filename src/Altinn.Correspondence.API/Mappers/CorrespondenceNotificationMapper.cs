using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Core.Models;

namespace Altinn.Correspondence.Mappers;

internal static class CorrespondenceNotificationMapper
{
    internal static CorrespondenceNotificationEntity MapToEntity(InitializeCorrespondenceNotificationExt correspondenceNotificationExt)
    {
        var notification = new CorrespondenceNotificationEntity
        {
            NotificationTemplate = correspondenceNotificationExt.NotificationTemplate,
            RequestedSendTime = correspondenceNotificationExt.RequestedSendTime,
            SendersReference = correspondenceNotificationExt.SendersReference,
            CustomTextToken = correspondenceNotificationExt.CustomTextToken
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
}
