using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace Altinn.Correspondence.Core.Models.Entities
{

    [Index(nameof(CorrespondenceId), IsUnique = true)]
    public class ConfidentialReminderEntity
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public string Recipient { get; set; } = null!;

        [Required]
        [ForeignKey("CorrespondenceId")]
        public Guid CorrespondenceId { get; set; }

        public Guid? DialogId { get; set; }
    }
}
