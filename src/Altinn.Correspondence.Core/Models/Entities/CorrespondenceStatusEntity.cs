using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Altinn.Correspondence.Core.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Altinn.Correspondence.Core.Models.Entities
{
    [Index(nameof(Status))]
    public class CorrespondenceStatusEntity
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public CorrespondenceStatus Status { get; set; }

        public string StatusText { get; set; } = string.Empty;

        [Required]
        public DateTimeOffset StatusChanged { get; set; }

        public Guid CorrespondenceId { get; set; }
        [ForeignKey("CorrespondenceId")]
        public CorrespondenceEntity? Correspondence { get; set; }
        public Guid PartyUuid { get; set; }

    }
}