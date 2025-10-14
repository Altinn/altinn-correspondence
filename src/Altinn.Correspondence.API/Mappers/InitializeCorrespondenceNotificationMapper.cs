using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.Application.InitializeCorrespondences;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Application;
using Altinn.Correspondence.API.Models.Enums;
using OneOf;

namespace Altinn.Correspondence.Mappers;

internal static class InitializeCorrespondenceNotificationMapper
{
    internal static OneOf<CorrespondenceNotificationEntity, Error> MapToEntity(InitializeCorrespondenceNotificationExt correspondenceNotificationExt)
    {
       var templateExt = correspondenceNotificationExt.NotificationTemplate;
        NotificationTemplate templateCore;
        var error = ValidateNotificationTemplate(templateExt);
        if (error != null)
        {
            return error;
        }

        if (templateExt.HasValue)
        {
            templateCore = (NotificationTemplate)(int)templateExt.Value;
        }
        else
        {
            templateCore = NotificationTemplate.CustomMessage;
        }

        var notification = new CorrespondenceNotificationEntity
        {
            NotificationTemplate = templateCore,
            NotificationChannel = (NotificationChannel)correspondenceNotificationExt.NotificationChannel,
            RequestedSendTime = correspondenceNotificationExt.RequestedSendTime ?? DateTimeOffset.UtcNow,
            Created = DateTimeOffset.UtcNow
        };
        return notification;
    }

    internal static OneOf<NotificationRequest, Error> MapToRequest(InitializeCorrespondenceNotificationExt correspondenceNotificationExt)
    {
        // Handle customRecipients - prioritize the new property
        List<Recipient>? customRecipients = null;

        if (correspondenceNotificationExt.CustomRecipients != null && correspondenceNotificationExt.CustomRecipients.Any())
        {
            // Use the new customRecipients property
            customRecipients = correspondenceNotificationExt.CustomRecipients.Select(recipient => new Recipient
            {
                EmailAddress = recipient.EmailAddress,
                IsReserved = recipient.IsReserved,
                MobileNumber = recipient.MobileNumber,
                NationalIdentityNumber = recipient.NationalIdentityNumber,
                OrganizationNumber = recipient.OrganizationNumber
            }).ToList();
        }
        else if (correspondenceNotificationExt.CustomRecipient != null)
        {
            // Map single customRecipient to customRecipients list for backward compatibility
            customRecipients = new List<Recipient>
            {
                new Recipient
                {
                    EmailAddress = correspondenceNotificationExt.CustomRecipient.EmailAddress,
                    IsReserved = correspondenceNotificationExt.CustomRecipient.IsReserved,
                    MobileNumber = correspondenceNotificationExt.CustomRecipient.MobileNumber,
                    NationalIdentityNumber = correspondenceNotificationExt.CustomRecipient.NationalIdentityNumber,
                    OrganizationNumber = correspondenceNotificationExt.CustomRecipient.OrganizationNumber
                }
            };
        }
        else if (correspondenceNotificationExt.CustomNotificationRecipients?.FirstOrDefault()?.Recipients.FirstOrDefault() != null)
        {
            // Map deprecated customNotificationRecipients to customRecipients list
            customRecipients = correspondenceNotificationExt.CustomNotificationRecipients.First().Recipients.Select(recipient => new Recipient
            {
                EmailAddress = recipient.EmailAddress,
                IsReserved = recipient.IsReserved,
                MobileNumber = recipient.MobileNumber,
                NationalIdentityNumber = recipient.NationalIdentityNumber,
                OrganizationNumber = recipient.OrganizationNumber
            }).ToList();
        }

        var templateExt = correspondenceNotificationExt.NotificationTemplate;
        NotificationTemplate templateCore;
        var error = ValidateNotificationTemplate(templateExt);
        if (error != null)
        {
            return error;
        }

        if (templateExt.HasValue)
        {
            templateCore = (NotificationTemplate)(int)templateExt.Value;
        }
        else
        {
            templateCore = NotificationTemplate.CustomMessage;
        }

        var notification = new NotificationRequest
        {
            NotificationTemplate = templateCore,
            NotificationChannel = (NotificationChannel)correspondenceNotificationExt.NotificationChannel,
            RequestedSendTime = correspondenceNotificationExt.RequestedSendTime,
            EmailBody = correspondenceNotificationExt.EmailBody,
            EmailSubject = correspondenceNotificationExt.EmailSubject,
            EmailContentType = correspondenceNotificationExt.EmailContentType,
            ReminderEmailBody = correspondenceNotificationExt.ReminderEmailBody,
            ReminderEmailSubject = correspondenceNotificationExt.ReminderEmailSubject,
            ReminderEmailContentType = correspondenceNotificationExt.ReminderEmailContentType,
            ReminderSmsBody = correspondenceNotificationExt.ReminderSmsBody,
            ReminderNotificationChannel = (NotificationChannel?)correspondenceNotificationExt.ReminderNotificationChannel,
            SmsBody = correspondenceNotificationExt.SmsBody,
            SendReminder = correspondenceNotificationExt.SendReminder,
            CustomRecipients = customRecipients,
            OverrideRegisteredContactInformation = correspondenceNotificationExt.OverrideRegisteredContactInformation
        };
        return notification;
    }
    private static Error? ValidateNotificationTemplate(NotificationTemplateExt? template)
    {
        if (template.HasValue && !Enum.IsDefined(typeof(NotificationTemplate), (int)template.Value))
        {
            return NotificationErrors.InvalidNotificationTemplate;
        }
        return null;
    }
}
