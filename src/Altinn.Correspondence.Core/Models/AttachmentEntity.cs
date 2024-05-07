using Altinn.Correspondence.Core.Models.Enums;
using System.ComponentModel.DataAnnotations;

namespace Altinn.Correspondence.Core.Models
{
    public class AttachmentEntity
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(255)]
        public string? FileName { get; set; }

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
    }
}