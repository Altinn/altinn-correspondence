using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Core.Models.Entities
{
    public class AttachmentStatusEntity
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public AttachmentStatus Status { get; set; }

        public string StatusText { get; set; } = string.Empty;

        [Required]
        public DateTimeOffset StatusChanged { get; set; }

        public Guid AttachmentId { get; set; }
        [ForeignKey("AttachmentId")]
        public AttachmentEntity? Attachment { get; set; }
        public Guid PartyUuid { get; set; }

    }
}