
using Altinn.Correspondence.Core.Models.Enums;
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

        public DateTimeOffset RequestedSendTime { get; set; }
    }
}
