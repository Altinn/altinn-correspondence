using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Core.Models.Notifications
{
    public class NotificationOrderRequest
    {
        public DateTime RequestedSendTime { get; set; } = DateTime.UtcNow;

        public string? SendersReference { get; set; }

        public List<Recipient> Recipients { get; set; } = new List<Recipient>();

        public bool? IgnoreReservation { get; set; }

        public string? ResourceId { get; set; }

        public Uri? ConditionEndpoint { get; set; }

        public NotificationChannel? NotificationChannel { get; set; }

        public EmailTemplate? EmailTemplate { get; set; }

        public SmsTemplate? SmsTemplate { get; set; }
    }
}