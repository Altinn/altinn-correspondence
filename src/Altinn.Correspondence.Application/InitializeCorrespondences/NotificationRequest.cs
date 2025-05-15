using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Models.Notifications;
namespace Altinn.Correspondence.Application.InitializeCorrespondences
{
    public class NotificationRequest
    {
        public required NotificationTemplate NotificationTemplate { get; set; }

        public string? EmailSubject { get; set; }

        public string? EmailBody { get; set; }

        public string? SmsBody { get; set; }

        public bool SendReminder { get; set; }

        public string? ReminderEmailSubject { get; set; }

        public string? ReminderEmailBody { get; set; }

        public string? ReminderSmsBody { get; set; }

        public required NotificationChannel NotificationChannel { get; set; }

        public NotificationChannel? ReminderNotificationChannel { get; set; }

        public DateTimeOffset? RequestedSendTime { get; set; }

        public Recipient? CustomRecipient { get; set; }
    }
}
