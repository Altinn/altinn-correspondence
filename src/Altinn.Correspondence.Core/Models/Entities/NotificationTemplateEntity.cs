using System.ComponentModel.DataAnnotations;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Core.Models.Entities
{
    public class NotificationTemplateEntity
    {
        [Key]
        public int Id { get; set; }

        public NotificationTemplate? Template { get; set; }
        public RecipientType? RecipientType { get; set; }
        public required string EmailSubject { get; set; }
        public required string EmailBody { get; set; }
        public required string SmsBody { get; set; }
        public required string ReminderEmailBody { get; set; }
        public required string ReminderEmailSubject { get; set; }
        public required string ReminderSmsBody { get; set; }
        public string? Language { get; set; }
    }
}