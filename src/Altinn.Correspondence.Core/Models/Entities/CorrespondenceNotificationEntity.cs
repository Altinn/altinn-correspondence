using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Core.Models.Entities
{
    public class CorrespondenceNotificationEntity
    {
        [Key]
        public Guid Id { get; set; }

        public Guid? NotificationOrderId { get; set; }

        public required NotificationTemplate NotificationTemplate { get; set; }

        public required NotificationChannel NotificationChannel { get; set; }

        public DateTimeOffset RequestedSendTime { get; set; }

        public Guid CorrespondenceId { get; set; }
        [ForeignKey("CorrespondenceId")]
        public CorrespondenceEntity? Correspondence { get; set; }

        [Required]
        public required DateTimeOffset Created { get; set; }

        public bool IsReminder { get; set; }

        public DateTimeOffset? NotificationSent { get; set; }

        public string? NotificationAddress { get; set; }

    }
}
