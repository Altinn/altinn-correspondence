using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Altinn.Correspondence.Core.Models
{
    public class CorrespondenceNotificationStatusEntity
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public string Status { get; set; }

        public string? StatusText { get; set; }

        [Required]
        public DateTimeOffset StatusChanged { get; set; }

        public Guid NotificationId { get; set; }
        [ForeignKey("NotificationId")]
        public CorrespondenceNotificationEntity Notification { get; set; }
    }
}
