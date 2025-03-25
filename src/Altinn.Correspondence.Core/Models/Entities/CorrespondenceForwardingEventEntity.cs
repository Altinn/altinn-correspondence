using Altinn.Correspondence.Common.Constants;
using System.ComponentModel.DataAnnotations;

namespace Altinn.Correspondence.Core.Models.Entities
{
    public class CorrespondenceForwardingEventEntity
    {
        [Key]
        public Guid Id { get; set; }

        public Guid CorrespondenceId { get; set; }

        [Required]
        public DateTimeOffset ForwardedOnDate { get; set; }

        [Required]
        public int ForwardedByUserId { get; set; }

        [Required]
        public Guid ForwardedByUserUuid { get; set; }        

        public Guid? ForwardedToUserId { get; set; }

        public Guid? ForwardedToUserUuid { get; set; }

        public string? ForwardingText { get; set; }

        public string? ForwardedToEmailAddress { get; set; }

        [RegularExpression($@"^(?:0192:|{UrnConstants.OrganizationNumberAttribute}):\d{{9}}$", ErrorMessage = "Organization numbers should be on the format countrycode:organizationnumber, for instance 0192:910753614")]
        public string? MailboxSupplier { get; set; }
    }
}
