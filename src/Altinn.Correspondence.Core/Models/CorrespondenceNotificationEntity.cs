using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Altinn.Correspondence.Core.Models
{
    public class CorrespondenceNotificationEntity
    {
        [Key]
        public Guid Id { get; set; }

        public string NotificationTemplate { get; set; }

        [StringLength(128, MinimumLength = 0)]
        public string? CustomTextToken { get; set; }

        public string? SendersReference { get; set; }

        public DateTimeOffset RequestedSendTime { get; set; }

        public Guid CorrespondenceId { get; set; }
        [ForeignKey("CorrespondenceId")]
        public CorrespondenceEntity Correspondence { get; set; }

        public DateTimeOffset Created { get; set; }

        public List<CorrespondenceNotificationStatusEntity> Statuses { get; set; }
    }
}
