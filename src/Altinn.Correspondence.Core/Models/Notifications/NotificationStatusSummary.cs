namespace Altinn.Correspondence.Core.Models.Notifications;

public class NotificationsStatusSummary
{
    public EmailNotificationStatus? Email { get; set; }

    public SmsNotificationStatus? Sms { get; set; }
}