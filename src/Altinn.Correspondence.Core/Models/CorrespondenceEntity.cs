using System.ComponentModel.DataAnnotations;

namespace Altinn.Correspondence.Core.Models
{
    public class CorrespondenceEntity
    {
        [Key]
        public Guid Id { get; set; }

        [StringLength(255, MinimumLength = 1)]
        [Required]
        public required string ResourceId { get; set; }

        [Required]
        public required string Recipient { get; set; }

        [RegularExpression(@"^\d{4}:\d{9}$", ErrorMessage = "Organization numbers should be on the form countrycode:organizationnumber, for instance 0192:910753614")]
        [Required]
        public required string Sender { get; set; }

        [StringLength(4096, MinimumLength = 1)]
        [Required]
        public required string SendersReference { get; set; }

        public CorrespondenceContentEntity Content { get; set; }

        public required DateTimeOffset VisibleFrom { get; set; }

        public DateTimeOffset? AllowSystemDeleteAfter { get; set; }

        public DateTimeOffset DueDateTime { get; set; }

        public List<ExternalReferenceEntity>? ExternalReferences { get; set; }

        [MaxLength(10, ErrorMessage = "propertyList can contain at most 10 properties")]
        public Dictionary<string, string> PropertyList { get; set; } = new Dictionary<string, string>();

        public List<CorrespondenceReplyOptionEntity>? ReplyOptions { get; set; }

        [MaxLength(6, ErrorMessage = "Notifications can contain at most 6 notifcations")]
        public List<CorrespondenceNotificationEntity>? Notifications { get; set; }

        public bool? IsReservable { get; set; }

        public List<CorrespondenceStatusEntity>? Statuses { get; set; }

        [Required]
        public required DateTimeOffset Created { get; set; }
    }
}