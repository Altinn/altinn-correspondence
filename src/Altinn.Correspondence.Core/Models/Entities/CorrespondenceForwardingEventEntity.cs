using Altinn.Correspondence.Common.Constants;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Altinn.Correspondence.Core.Models.Entities
{
    public class CorrespondenceForwardingEventEntity
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public DateTimeOffset ForwardedOnDate { get; set; }

        [Required]
        public Guid ForwardedByPartyUuid { get; set; }
        
        public int ForwardedByUserId { get; set; }

        [Required]
        public Guid ForwardedByUserUuid { get; set; }

        public int? ForwardedToUserId { get; set; }

        public Guid? ForwardedToUserUuid { get; set; }

        [MaxLength(4000)]
        public string? ForwardingText { get; set; }

        [MaxLength(1000)]
        public string? ForwardedToEmailAddress { get; set; }

        [RegularExpression($@"^(?:0192:|{UrnConstants.OrganizationNumberAttribute}):\d{{9}}$", ErrorMessage = "Organization numbers should be on the format countrycode:organizationnumber, for instance 0192:910753614")]
        public string? MailboxSupplier { get; set; }

        public Guid CorrespondenceId { get; set; }

        [ForeignKey("CorrespondenceId")]
        public CorrespondenceEntity? Correspondence { get; set; }

        public DateTimeOffset? SyncedFromAltinn2 { get; set; }
    }
}
