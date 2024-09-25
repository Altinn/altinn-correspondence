using Altinn.Correspondence.API.Models;
using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Core.Models.Notifications;

namespace Altinn.Correspondence.Mappers;

internal static class NotificationMapper
{
    internal static NotificationExt MapToExternal(NotificationStatusResponse notification)
    {
        var notificationExt = new NotificationExt
        {
            NotificationChannel = (NotificationChannelExt)notification.NotificationChannel,
            RequestedSendTime = notification.RequestedSendTime,
            Created = notification.Created,
            Id = notification.Id,
            Creator = notification.Creator,
            IgnoreReservation = notification.IgnoreReservation,
            NotificationStatusDetails = notification.NotificationsStatusDetails != null ? MapNotificationsStatusSummaryExtToExternal(notification.NotificationsStatusDetails) : null,
            ProcessingStatus = MapStatusToExternal(notification.ProcessingStatus),
            ResourceId = notification.ResourceId,
            SendersReference = notification.SendersReference
        };
        return notificationExt;
    }

    internal static List<NotificationExt> MapListToExternal(List<NotificationStatusResponse> notifications)
    {
        var notificationsExt = new List<NotificationExt>();
        foreach (var not in notifications)
        {
            notificationsExt.Add(MapToExternal(not));
        }
        return notificationsExt;
    }

    private static NotificationProcessStatusExt MapStatusToExternal(StatusExt status)
    {
        return new NotificationProcessStatusExt()
        {
            LastUpdate = status.LastUpdate,
            Status = status.Status,
            StatusDescription = status.StatusDescription
        };
    }
    private static NotificationStatusDetailsExt MapNotificationsStatusSummaryExtToExternal(NotificationsStatusDetails status)
    {
        var Emails = new List<NotificationDetailsExt>();
        if (status.Email != null)
        {
            foreach (var email in status.Email)
            {
                Emails.Add(new NotificationDetailsExt()
                {
                    Id = email.Id,
                    Recipient = new NotificationRecipientExt()
                    {
                        EmailAddress = email.Recipient.EmailAddress,
                        IsReserved = email.Recipient.IsReserved,
                        MobileNumber = email.Recipient.MobileNumber,
                        NationalIdentityNumber = email.Recipient.NationalIdentityNumber,
                        OrganizationNumber = email.Recipient.OrganizationNumber
                    },
                    SendStatus = new NotificationStatusExt()
                    {
                        LastUpdate = email.SendStatus.LastUpdate,
                        Status = email.SendStatus.Status,
                        StatusDescription = email.SendStatus.StatusDescription
                    },
                    Succeeded = email.Succeeded
                });
            }
        }
        var Smses = new List<NotificationDetailsExt>();
        if (status.Sms != null)
        {
            foreach (var sms in status.Sms)
            {
                Smses.Add(new NotificationDetailsExt()
                {
                    Id = sms.Id,
                    Recipient = new NotificationRecipientExt()
                    {
                        EmailAddress = sms.Recipient.EmailAddress,
                        IsReserved = sms.Recipient.IsReserved,
                        MobileNumber = sms.Recipient.MobileNumber,
                        NationalIdentityNumber = sms.Recipient.NationalIdentityNumber,
                        OrganizationNumber = sms.Recipient.OrganizationNumber
                    },
                    SendStatus = new NotificationStatusExt()
                    {
                        LastUpdate = sms.SendStatus.LastUpdate,
                        Status = sms.SendStatus.Status,
                        StatusDescription = sms.SendStatus.StatusDescription
                    },
                    Succeeded = sms.Succeeded
                });
            }
        }
        return new NotificationStatusDetailsExt()
        {
            Email = Emails,
            Sms = Smses
        };
    }
}
