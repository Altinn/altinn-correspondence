using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Altinn.Correspondence.Core.Models
{
    public class CorrespondenceNotificationStatusEntity
    {
        [Key]
        public Guid Id { get; set; }

        public string Status { get; set; }

        public string? StatusText { get; set; }

        public DateTimeOffset StatusChanged { get; set; }

        public Guid NotificationId { get; set; }
        [ForeignKey("NotificationId")]
        public CorrespondenceNotificationEntity Notification { get; set; }
    }
}
