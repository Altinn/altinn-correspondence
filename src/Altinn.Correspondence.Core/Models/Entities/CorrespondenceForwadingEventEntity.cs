using Altinn.Correspondence.Common.Constants;
using System.ComponentModel.DataAnnotations;

namespace Altinn.Correspondence.Core.Models.Entities
{
    public class CorrespondenceForwadingEventEntity
    {
        [Key]
        public Guid Id { get; set; }

        [Required]
        public DateTimeOffset ForwardedOnDate { get; set; }

        [Required]
        public Guid ForwardedByUserPartyUuid { get; set; }

        public Guid? ForwardedToUserPartyUuid { get; set; }

        public string? ForwardingText { get; set; }

        public string? ForwardedToEmailAddress { get; set; }

        [RegularExpression($@"^(?:0192:|{UrnConstants.OrganizationNumberAttribute}):\d{{9}}$", ErrorMessage = "Organization numbers should be on the format countrycode:organizationnumber, for instance 0192:910753614")]
        public string? MailboxSupplier { get; set; }
    }
}
