namespace Altinn.Correspondence.Core.Models.Notifications;

public class NotificationsStatusDetails
{
    public List<EmailNotificationWithResult>? Email { get; set; }

    public List<SmsNotificationWithResult>? Sms { get; set; }
}