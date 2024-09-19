using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application.InitializeCorrespondences;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Mappers;

internal static class InitializeCorrespondenceNotificationMapper
{
    internal static CorrespondenceNotificationEntity MapToEntity(InitializeCorrespondenceNotificationExt correspondenceNotificationExt)
    {
        var notification = new CorrespondenceNotificationEntity
        {
            NotificationTemplate = (NotificationTemplate)correspondenceNotificationExt.NotificationTemplate,
            NotificationChannel = (NotificationChannel)correspondenceNotificationExt.NotificationChannel,
            RequestedSendTime = correspondenceNotificationExt.RequestedSendTime,
            Created = DateTimeOffset.UtcNow
        };
        return notification;
    }

    internal static CorrespondenceNotificationExt MapToExternal(CorrespondenceNotificationEntity correspondenceNotification)
    {
        var notification = new CorrespondenceNotificationExt
        {
            NotificationTemplate = (NotificationTemplateExt)correspondenceNotification.NotificationTemplate,
            NotificationChannel = (NotificationChannelExt)correspondenceNotification.NotificationChannel,
            RequestedSendTime = correspondenceNotification.RequestedSendTime,
            Created = correspondenceNotification.Created,
            Id = correspondenceNotification.Id,
            NotificationOrderId = correspondenceNotification.NotificationOrderId,
        };
        return notification;
    }
    internal static NotificationRequest MapToRequest(InitializeCorrespondenceNotificationExt correspondenceNotificationExt)
    {
        var notification = new NotificationRequest
        {
            NotificationTemplate = (NotificationTemplate)correspondenceNotificationExt.NotificationTemplate,
            NotificationChannel = (NotificationChannel)correspondenceNotificationExt.NotificationChannel,
            RequestedSendTime = correspondenceNotificationExt.RequestedSendTime,
            EmailBody = correspondenceNotificationExt.EmailBody,
            EmailSubject = correspondenceNotificationExt.EmailSubject,
            ReminderEmailBody = correspondenceNotificationExt.ReminderEmailBody,
            ReminderEmailSubject = correspondenceNotificationExt.ReminderEmailSubject,
            ReminderSmsBody = correspondenceNotificationExt.ReminderSmsBody,
            SmsBody = correspondenceNotificationExt.SmsBody,
            SendReminder = correspondenceNotificationExt.SendReminder
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
