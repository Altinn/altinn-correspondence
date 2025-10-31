using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Core.Models.Enums;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace Altinn.Correspondence.Core.Models.Entities
{
    [Index(nameof(Altinn2AttachmentId), IsUnique = true)]
    [Index(nameof(DataLocationUrl))]
    [Index(nameof(ServiceOwnerId))]
    public class AttachmentEntity
    {
        [Key]
        public Guid Id { get; set; }

        [StringLength(255, MinimumLength = 1)]
        [Required]
        public required string ResourceId { get; set; }

        [MaxLength(255)]
        public string? FileName { get; set; }

        [MaxLength(255)]
        public string? DisplayName { get; set; }

        public bool IsEncrypted { get; set; }

        public string? Checksum { get; set; } = string.Empty;

        [MaxLength(4096)]
        [MinLength(1)]
        [Required]
        public required string SendersReference { get; set; }

        [Required]
        [RegularExpression($@"^(?:0192:|{UrnConstants.OrganizationNumberAttribute}):\d{{9}}$", ErrorMessage = "Organization numbers should be on the format countrycode:organizationnumber, for instance 0192:910753614")]
        public required string Sender { get; set; }

        public List<AttachmentStatusEntity> Statuses { get; set; } = new List<AttachmentStatusEntity>();

        public string? DataLocationUrl { get; set; }

        public AttachmentDataLocationType DataLocationType { get; set; }

        public List<CorrespondenceAttachmentEntity> CorrespondenceAttachments { get; set; } = new List<CorrespondenceAttachmentEntity>();

        [Required]
        public required DateTimeOffset Created { get; set; }

        [Required]
        public long AttachmentSize { get; set; }

        public DateTimeOffset? ExpirationTime { get; set; }

        public StorageProviderEntity? StorageProvider { get; set; }

        public string? Altinn2AttachmentId { get; set; }

        /// <summary>
        /// Foreign key reference to ServiceOwner table. 
        /// Contains the organization number without prefix.
        /// </summary>
        public string? ServiceOwnerId { get; set; }

        /// <summary>
        /// Status of the service owner migration.
        /// 0: Not started / pending
        /// 1: Completed (migration completed successfully with service owner)
        /// 2: Completed (migration completed successfully without service owner)
        /// This field is temporary and will be removed when the migration is complete.
        /// </summary>
        public int ServiceOwnerMigrationStatus { get; set; } = 0;
    }
}