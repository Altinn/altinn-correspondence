using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Altinn.Correspondence.Core.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace Altinn.Correspondence.Core.Models.Entities
{
    [Index(nameof(EventType))]
    public class CorrespondenceDeleteEventEntity
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public CorrespondenceDeleteEventType EventType { get; set; }

        [Required]
        public DateTimeOffset EventOccurred { get; set; }

        [Required]
        public Guid CorrespondenceId { get; set; }
        
        [ForeignKey("CorrespondenceId")]
        public CorrespondenceEntity? Correspondence { get; set; }
        
        public Guid PartyUuid { get; set; }

        public DateTimeOffset? SyncedFromAltinn2 { get; set; }
    }
}