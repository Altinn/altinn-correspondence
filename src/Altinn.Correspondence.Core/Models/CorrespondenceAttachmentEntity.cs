using Altinn.Correspondence.Core.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Altinn.Correspondence.Core.Models
{
    public class CorrespondenceAttachmentEntity
    {
        [Key]
        public Guid Id { get; set; }

        [MaxLength(255)]
        [Required]
        public required string Name { get; set; }

        [Required]
        public string RestrictionName { get; set; } = string.Empty;

        [Required]
        public DateTimeOffset ExpirationTime { get; set; }

        public Guid CorrespondenceContentId { get; set; }
        [ForeignKey("CorrespondenceContentId")]
        public CorrespondenceContentEntity? CorrespondenceContent { get; set; }

        public Guid AttachmentId { get; set; }
        [ForeignKey("AttachmentId")]
        public AttachmentEntity? Attachment { get; set; }

    }
}