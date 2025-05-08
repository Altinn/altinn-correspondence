using System.Text.Json.Serialization;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Core.Models.Notifications
{
    public class NotificationOrderRequestV2
    {
        public string? SendersReference { get; set; }

        public DateTime RequestedSendTime { get; set; }

        public DialogportenAssociation? DialogportenAssociation { get; set; }

        public Guid IdempotencyId { get; set; }

        public RecipientV2 Recipient { get; set; } = null!;

        public List<ReminderV2>? Reminders { get; set; }
    }

    public class DialogportenAssociation
    {
        public string DialogId { get; set; } = null!;

        public string TransmissionId { get; set; } = null!;
    }

    public class RecipientV2
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public RecipientOrganization? RecipientOrganization { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public RecipientPerson? RecipientPerson { get; set; }
    }

    public class RecipientOrganization
    {
        public string OrgNumber { get; set; } = null!;

        public string ResourceId { get; set; } = null!;

        public string ChannelSchema { get; set; } = null!;

        public EmailSettings? EmailSettings { get; set; }

        public SmsSettings? SmsSettings { get; set; }
    }

    public class RecipientPerson
    {
        public string ResourceId { get; set; } = null!;

        public NotificationChannel ChannelSchema { get; set;}

        public EmailSettings? EmailSettings { get; set; }

        public SmsSettings? SmsSettings { get; set; }
    }

    public class EmailSettings
    {
        public string Subject { get; set; } = null!;

        public string Body { get; set; } = null!;
    }

    public class SmsSettings
    {
        public string Body { get; set; } = null!;
    }

    public class ReminderV2
    {
        public string? SendersReference { get; set; }

        public int DelayDays { get; set; }

        public RecipientV2 Recipient { get; set; } = null!;
    }
} 