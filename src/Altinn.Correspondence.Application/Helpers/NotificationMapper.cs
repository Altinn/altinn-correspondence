using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Models.Notifications;
using Altinn.Correspondence.Core.Services;

namespace Altinn.Correspondence.Application.Helpers;

public class NotificationMapper
{
    private readonly IResourceRegistryService _resourceRegistryService;

    public NotificationMapper(IResourceRegistryService resourceRegistryService)
    {
        _resourceRegistryService = resourceRegistryService;
    }

    public async Task<NotificationStatusResponse> MapAltinn2NotificationToAltinn3NotificationStatus(CorrespondenceNotificationEntity notification)
    {
        var correspondence = notification.Correspondence ?? throw new ArgumentException($"Correspondence with id {notification.CorrespondenceId} not found when mapping notification", nameof(notification));
        NotificationsStatusDetails nsd = new NotificationsStatusDetails();
        var sendStatus = new StatusExt()
        {
            Status = "Completed",
            LastUpdate = notification.NotificationSent.Value.UtcDateTime
        };

        if (notification.NotificationChannel == Core.Models.Enums.NotificationChannel.Sms)
        {
            nsd.Sms = new SmsNotificationWithResult()
            {
                Recipient = new Recipient()
                {
                    MobileNumber = notification.NotificationAddress
                },
                SendStatus = sendStatus,
                Succeeded = true
            };
        }
        else
        {
            nsd.Email = new EmailNotificationWithResult()
            {
                Recipient = new Recipient()
                {
                    EmailAddress = notification.NotificationAddress
                },
                SendStatus = sendStatus,
                Succeeded = true
            };
        }

        return new NotificationStatusResponse
        {
            SendersReference = null,
            RequestedSendTime = notification.RequestedSendTime.DateTime,
            Created = notification.Created.DateTime,
            Creator = correspondence?.ResourceId != null ? await _resourceRegistryService.GetServiceOwnerOrgCode(correspondence.ResourceId) : "Not found",
            IsReminder = notification.IsReminder,
            NotificationChannel = notification.NotificationChannel,
            ResourceId = correspondence.ResourceId,
            IgnoreReservation = correspondence.IgnoreReservation ?? false,
            ProcessingStatus = sendStatus,
            NotificationsStatusDetails = nsd
        };
    }

    public async Task<NotificationStatusResponse> MapNotificationV2ToV1Async(NotificationStatusResponseV2 notificationDetails, CorrespondenceNotificationEntity notification)
    {
        var correspondence = notification.Correspondence ?? throw new ArgumentException($"Correspondence with id {notification.CorrespondenceId} not found when mapping notification", nameof(notification));
        var latestEmailRecipient = notificationDetails.Recipients
            .Where(r => r.Type == NotificationType.Email)
            .OrderByDescending(r => r.LastUpdate)
            .FirstOrDefault();

        var latestSmsRecipient = notificationDetails.Recipients
            .Where(r => r.Type == NotificationType.SMS)
            .OrderByDescending(r => r.LastUpdate)
            .FirstOrDefault();

        var emailRecipients = notificationDetails.Recipients
            .Where(r => r.Type == NotificationType.Email)
            .OrderByDescending(r => r.LastUpdate)
            .ToList();

        var smsRecipients = notificationDetails.Recipients
            .Where(r => r.Type == NotificationType.SMS)
            .OrderByDescending(r => r.LastUpdate)
            .ToList();

        return new NotificationStatusResponse
        {
            Id = notificationDetails.ShipmentId.ToString(),
            SendersReference = notificationDetails.SendersReference,
            RequestedSendTime = notification.RequestedSendTime.DateTime,
            Created = notification.Created.DateTime,
            Creator = correspondence?.ResourceId != null ? await _resourceRegistryService.GetServiceOwnerOrgCode(correspondence.ResourceId) : "Not found",
            IsReminder = notification.IsReminder,
            NotificationChannel = notification.NotificationChannel,
            ResourceId = correspondence.ResourceId,
            IgnoreReservation = correspondence.IgnoreReservation ?? false,
            ProcessingStatus = new StatusExt
            {
                Status = notificationDetails.Status,
                LastUpdate = notificationDetails.LastUpdate.DateTime
            },
            NotificationsStatusDetails = new NotificationsStatusDetails
            {
                Email = latestEmailRecipient != null ? new EmailNotificationWithResult
                {
                    Recipient = new Recipient
                    {
                        EmailAddress = latestEmailRecipient?.Destination
                    },
                    SendStatus = new StatusExt
                    {
                        Status = latestEmailRecipient?.Status.ToString() ?? string.Empty,
                        LastUpdate = latestEmailRecipient?.LastUpdate.DateTime ?? DateTime.MinValue
                    },
                    Succeeded = latestEmailRecipient?.IsSent() ?? false
                } : null,
                Sms = latestSmsRecipient != null ? new SmsNotificationWithResult
                {
                    Recipient = new Recipient
                    {
                        MobileNumber = latestSmsRecipient?.Destination
                    },
                    SendStatus = new StatusExt
                    {
                        Status = latestSmsRecipient?.Status.ToString() ?? string.Empty,
                        LastUpdate = latestSmsRecipient?.LastUpdate.DateTime ?? DateTime.MinValue
                    },
                    Succeeded = latestSmsRecipient?.IsSent() ?? false
                } : null,
                Emails = emailRecipients != null && emailRecipients.Count != 0 ? [.. emailRecipients.Select(r => new EmailNotificationWithResult
                {
                    Recipient = new Recipient
                    {
                        EmailAddress = r.Destination
                    },
                    SendStatus = new StatusExt
                    {
                        Status = r.Status.ToString(),
                        LastUpdate = r.LastUpdate.DateTime
                    },
                    Succeeded = r.IsSent()
                })] : null,
                Smses = smsRecipients != null && smsRecipients.Count != 0 ? [.. smsRecipients.Select(r => new SmsNotificationWithResult
                {
                    Recipient = new Recipient
                    {
                        MobileNumber = r.Destination
                    },
                    SendStatus = new StatusExt
                    {
                        Status = r.Status.ToString(),
                        LastUpdate = r.LastUpdate.DateTime
                    },
                    Succeeded = r.IsSent() 
                })] : null,
            }
        };
    }
} 