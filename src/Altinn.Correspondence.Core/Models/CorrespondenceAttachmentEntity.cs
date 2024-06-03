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

        public bool IsEncrypted { get; set; }

        public string? Checksum { get; set; } = string.Empty;

        [MaxLength(4096)]
        [MinLength(1)]
        [Required]
        public required string SendersReference { get; set; }

        [Required]
        public required string DataType { get; set; }

        [Required]
        public required IntendedPresentationType IntendedPresentation { get; set; }

        [Required]
        public string RestrictionName { get; set; } = string.Empty;

        [Required]
        public DateTimeOffset ExpirationTime { get; set; }

        public string? DataLocationUrl { get; set; }

        public AttachmentDataLocationType DataLocationType { get; set; }

        public Guid CorrespondenceContentId { get; set; }
        [ForeignKey("CorrespondenceContentId")]
        public CorrespondenceContentEntity CorrespondenceContent { get; set; }

        public Guid AttachmentId { get; set; }
        [ForeignKey("AttachmentId")]
        public AttachmentEntity Attachment { get; set; }

    }
}