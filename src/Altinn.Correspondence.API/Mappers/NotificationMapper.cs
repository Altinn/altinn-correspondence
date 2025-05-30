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
            IsReminder = notification.IsReminder,
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
        return new NotificationStatusDetailsExt()
        {
            Email = status.Email != null ? new NotificationDetailsExt()
            {
                Id = status.Email.Id,
                Recipient = new NotificationRecipientExt()
                {
                    EmailAddress = status.Email.Recipient.EmailAddress,
                    IsReserved = status.Email.Recipient.IsReserved,
                    MobileNumber = status.Email.Recipient.MobileNumber,
                    NationalIdentityNumber = status.Email.Recipient.NationalIdentityNumber,
                    OrganizationNumber = status.Email.Recipient.OrganizationNumber
                },
                SendStatus = new NotificationStatusExt()
                {
                    LastUpdate = status.Email.SendStatus.LastUpdate,
                    Status = status.Email.SendStatus.Status,
                    StatusDescription = status.Email.SendStatus.StatusDescription
                },
                Succeeded = status.Email.Succeeded
            } : null,
            Sms = status.Sms != null ? new NotificationDetailsExt()
            {
                Id = status.Sms.Id,
                Recipient = new NotificationRecipientExt()
                {
                    EmailAddress = status.Sms.Recipient.EmailAddress,
                    IsReserved = status.Sms.Recipient.IsReserved,
                    MobileNumber = status.Sms.Recipient.MobileNumber,
                    NationalIdentityNumber = status.Sms.Recipient.NationalIdentityNumber,
                    OrganizationNumber = status.Sms.Recipient.OrganizationNumber
                },
                SendStatus = new NotificationStatusExt()
                {
                    LastUpdate = status.Sms.SendStatus.LastUpdate,
                    Status = status.Sms.SendStatus.Status,
                    StatusDescription = status.Sms.SendStatus.StatusDescription
                },
                Succeeded = status.Sms.Succeeded
            } : null,
            Emails = status.Emails != null ? [.. status.Emails.Select(e => new NotificationDetailsExt()
            {
                Id = e.Id,
                Recipient = new NotificationRecipientExt()
                {
                    EmailAddress = e.Recipient.EmailAddress,
                    IsReserved = e.Recipient.IsReserved,
                    MobileNumber = e.Recipient.MobileNumber,
                    NationalIdentityNumber = e.Recipient.NationalIdentityNumber,
                    OrganizationNumber = e.Recipient.OrganizationNumber
                },
                SendStatus = new NotificationStatusExt()
                {
                    LastUpdate = e.SendStatus.LastUpdate,
                    Status = e.SendStatus.Status,
                    StatusDescription = e.SendStatus.StatusDescription
                },
                Succeeded = e.Succeeded
            })] : null, 
            Smses = status.Smses != null ? [.. status.Smses.Select(s => new NotificationDetailsExt()
            {
                Id = s.Id,
                Recipient = new NotificationRecipientExt()
                {
                    EmailAddress = s.Recipient.EmailAddress,
                    IsReserved = s.Recipient.IsReserved,
                    MobileNumber = s.Recipient.MobileNumber,
                    NationalIdentityNumber = s.Recipient.NationalIdentityNumber,
                    OrganizationNumber = s.Recipient.OrganizationNumber
                },
                SendStatus = new NotificationStatusExt()
                {
                    LastUpdate = s.SendStatus.LastUpdate,
                    Status = s.SendStatus.Status,
                    StatusDescription = s.SendStatus.StatusDescription
                },
                Succeeded = s.Succeeded
            })] : null,
        };
    }
}
