using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Altinn.Correspondence.Core.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Altinn.Correspondence.Core.Models
{
    public class AttachmentStatusEntity
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public AttachmentStatus Status { get; set; }

        public string StatusText { get; set; } = string.Empty;

        public DateTimeOffset StatusChanged { get; set; }

        public int AttachmentId { get; set; }
        [ForeignKey("AttachmentId")]
        public AttachmentEntity Attachment { get; set; }

    }
}