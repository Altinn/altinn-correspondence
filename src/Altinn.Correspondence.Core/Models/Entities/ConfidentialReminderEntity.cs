using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Altinn.Correspondence.Core.Models.Entities
{

    public class ConfidentialReminderEntity
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public string Recipient { get; set; } = null!;

        [Required]
        [ForeignKey("CorrespondenceId")]
        public Guid CorrespondenceId { get; set; }

        [Required]
        public Guid DialogId { get; set; }
    }
}
