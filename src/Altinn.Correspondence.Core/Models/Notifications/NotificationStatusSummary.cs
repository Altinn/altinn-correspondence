namespace Altinn.Correspondence.Core.Models.Notifications;

/// <summary>
/// A class representing a summary of status overviews of all notification channels
/// </summary>
public class NotificationsStatusSummary
{
    public EmailNotificationStatusExt? Email { get; set; }

    public SmsNotificationStatusExt? Sms { get; set; }
}