namespace Altinn.Correspondence.Core.Models.Notifications;

public class NotificationsStatusDetails
{
    public EmailNotificationWithResult? Email { get; set; }

    public SmsNotificationWithResult? Sms { get; set; }

    public List<EmailNotificationWithResult> Emails { get; set; }

    public List<SmsNotificationWithResult> Smses { get; set; }
}