using Altinn.Correspondence.Core.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace Altinn.Correspondence.Core.Models
{
    public class AttachmentEntity
    {
        [Key]
        public Guid Id { get; set; }

        [StringLength(255, MinimumLength = 1)]
        [Required]
        public required string ResourceId { get; set; }

        [MaxLength(255)]
        public string? FileName { get; set; }

        public bool IsEncrypted { get; set; }

        public string? Checksum { get; set; } = string.Empty;

        [MaxLength(4096)]
        [MinLength(1)]
        [Required]
        public required string SendersReference { get; set; }

        [RegularExpression(@"^\d{4}:\d{9}$", ErrorMessage = "Organization numbers should be on the form countrycode:organizationnumber, for instance 0192:910753614")]
        [Required]
        public required string Sender { get; set; }

        [Required]
        public required string DataType { get; set; }

        [Required]
        public string RestrictionName { get; set; } = string.Empty;

        [Required]
        public DateTimeOffset ExpirationTime { get; set; }

        public List<AttachmentStatusEntity> Statuses { get; set; } = new List<AttachmentStatusEntity>();

        public string? DataLocationUrl { get; set; }

        public AttachmentDataLocationType DataLocationType { get; set; }

        public List<CorrespondenceAttachmentEntity> CorrespondenceAttachments { get; set; } = new List<CorrespondenceAttachmentEntity>();

        [Required]
        public required DateTimeOffset Created { get; set; }

    }
}