using System.ComponentModel.DataAnnotations;
using Altinn.Correspondence.Common.Constants;

namespace Altinn.Correspondence.Common.Helpers.Models
{

    public class ConfidentialReminderDialogDto
    {
        [Key]
        public Guid Id { get; set; }

        [StringLength(255, MinimumLength = 1)]
        [Required]
        public required string ResourceId { get; set; }

        [Required]
        public required string Recipient { get; set; }

        [Required]
        [RegularExpression($@"^(?:0192:|{UrnConstants.OrganizationNumberAttribute}):\d{{9}}$", ErrorMessage = "Organization numbers should be on the format countrycode:organizationnumber, for instance 0192:910753614")]
        public required string Sender { get; set; }

        [StringLength(4096, MinimumLength = 1)]
        [Required]
        public required string SendersReference { get; set; }

        [MaxLength(10, ErrorMessage = "propertyList can contain at most 10 properties")]
        public Dictionary<string, string> PropertyList { get; set; } = new Dictionary<string, string>();

        [StringLength(256, MinimumLength = 0)]
        public string? MessageSender { get; set; }

        [Required]
        public required DateTimeOffset Created { get; set; }

        public string? Title { get; set; }

        public string? Summary { get; set; }
        public string? Status { get; set; }
    }
}
