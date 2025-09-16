using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Models.Notifications;
namespace Altinn.Correspondence.Application.InitializeCorrespondences
{
    public class NotificationRequest
    {
        public required NotificationTemplate NotificationTemplate { get; set; }

        public string? EmailSubject { get; set; }

        public string? EmailBody { get; set; }

        public EmailContentType EmailContentType { get; set; } = EmailContentType.Plain;

        public string? SmsBody { get; set; }

        public bool SendReminder { get; set; }

        public string? ReminderEmailSubject { get; set; }

        public string? ReminderEmailBody { get; set; }

        public EmailContentType? ReminderEmailContentType { get; set; }

        public string? ReminderSmsBody { get; set; }

        public required NotificationChannel NotificationChannel { get; set; }

        public NotificationChannel? ReminderNotificationChannel { get; set; }

        public DateTimeOffset? RequestedSendTime { get; set; }

        public List<Recipient>? CustomRecipients { get; set; }

        /// <summary>
        /// When set to true, only CustomRecipients will be used for notifications, overriding the default correspondence recipient.
        /// This flag can only be used when CustomRecipients is provided.
        /// Default value is false (use default contact info + custom recipients).
        /// </summary>
        public bool OverrideKoFuVi { get; set; } = false;
    }
}
