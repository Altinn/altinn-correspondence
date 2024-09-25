namespace Altinn.Correspondence.Core.Models.Notifications;

public class NotificationsStatusDetails
{
    public EmailNotificationWithResult? Email { get; set; }

    public SmsNotificationWithResult? Sms { get; set; }
}