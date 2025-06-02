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
        // If CustomRecipient is not set but CustomNotificationRecipients is, transform the first recipient
        // This code should be refactored when we finish with deprecating CustomNotificationRecipients
        Recipient? customRecipient = correspondenceNotificationExt.CustomRecipient != null 
            ? new Recipient
            {
                EmailAddress = correspondenceNotificationExt.CustomRecipient.EmailAddress,
                IsReserved = correspondenceNotificationExt.CustomRecipient.IsReserved,
                MobileNumber = correspondenceNotificationExt.CustomRecipient.MobileNumber,
                NationalIdentityNumber = correspondenceNotificationExt.CustomRecipient.NationalIdentityNumber,
                OrganizationNumber = correspondenceNotificationExt.CustomRecipient.OrganizationNumber
            }
            : correspondenceNotificationExt.CustomNotificationRecipients?.FirstOrDefault()?.Recipients.FirstOrDefault() != null
                ? new Recipient
                {
                    EmailAddress = correspondenceNotificationExt.CustomNotificationRecipients.First().Recipients.First().EmailAddress,
                    IsReserved = correspondenceNotificationExt.CustomNotificationRecipients.First().Recipients.First().IsReserved,
                    MobileNumber = correspondenceNotificationExt.CustomNotificationRecipients.First().Recipients.First().MobileNumber,
                    NationalIdentityNumber = correspondenceNotificationExt.CustomNotificationRecipients.First().Recipients.First().NationalIdentityNumber,
                    OrganizationNumber = correspondenceNotificationExt.CustomNotificationRecipients.First().Recipients.First().OrganizationNumber
                }
                : null;


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
            CustomRecipient = customRecipient
        };
        return notification;
    }
}
