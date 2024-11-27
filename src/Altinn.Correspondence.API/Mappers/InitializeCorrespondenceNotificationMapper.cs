using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application.InitializeCorrespondences;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Models.Notifications;

namespace Altinn.Correspondence.Mappers;

internal static class InitializeCorrespondenceNotificationMapper
{
    internal static CorrespondenceNotificationEntity MapToEntity(InitializeCorrespondenceNotificationExt correspondenceNotificationExt)
    {
        var notification = new CorrespondenceNotificationEntity
        {
            NotificationTemplate = (NotificationTemplate)correspondenceNotificationExt.NotificationTemplate,
            NotificationChannel = (NotificationChannel)correspondenceNotificationExt.NotificationChannel,
            RequestedSendTime = correspondenceNotificationExt.RequestedSendTime ?? DateTimeOffset.UtcNow,
            Created = DateTimeOffset.UtcNow
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
            ReminderNotificationChannel = (NotificationChannel?)correspondenceNotificationExt.ReminderNotificationChannel,
            SmsBody = correspondenceNotificationExt.SmsBody,
            SendReminder = correspondenceNotificationExt.SendReminder,
            Recipients = NotificationMapper.MapExternalRecipientsToRequest(correspondenceNotificationExt.Recipients) ?? new List<Recipient>(),
        };
        return notification;
    }
}
