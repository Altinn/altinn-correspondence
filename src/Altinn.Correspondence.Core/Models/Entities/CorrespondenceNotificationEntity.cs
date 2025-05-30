using Altinn.Correspondence.Core.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Altinn.Correspondence.Core.Models.Entities
{
    public class CorrespondenceNotificationEntity
    {
        [Key]
        public Guid Id { get; set; }

        public Guid? NotificationOrderId { get; set; }

        public Guid? ShipmentId { get; set; }

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

        public int? Altinn2NotificationId { get; set; }

        public string? OrderRequest { get; set; }
    }
}
