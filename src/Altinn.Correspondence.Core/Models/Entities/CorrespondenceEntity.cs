using System;
using System.ComponentModel.DataAnnotations;
using Altinn.Correspondence.Common.Constants;
using Microsoft.EntityFrameworkCore;

namespace Altinn.Correspondence.Core.Models.Entities
{
    [Index(nameof(ResourceId))]
    [Index(nameof(Recipient))]
    [Index(nameof(Sender))]
    [Index(nameof(Created))]
    public class CorrespondenceEntity
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

        [StringLength(256, MinimumLength = 0)]
        public string? MessageSender { get; set; }

        public CorrespondenceContentEntity? Content { get; set; }

        public required DateTimeOffset RequestedPublishTime { get; set; }

        public DateTimeOffset? AllowSystemDeleteAfter { get; set; }

        public DateTimeOffset? DueDateTime { get; set; }

        public List<ExternalReferenceEntity> ExternalReferences { get; set; } = new List<ExternalReferenceEntity>();

        [MaxLength(10, ErrorMessage = "propertyList can contain at most 10 properties")]
        public Dictionary<string, string> PropertyList { get; set; } = new Dictionary<string, string>();

        public List<CorrespondenceReplyOptionEntity> ReplyOptions { get; set; } = new List<CorrespondenceReplyOptionEntity>();

        [MaxLength(6, ErrorMessage = "Notifications can contain at most 6 notifcations")]
        public List<CorrespondenceNotificationEntity> Notifications { get; set; } = new List<CorrespondenceNotificationEntity>();

        public bool? IgnoreReservation { get; set; }

        public required List<CorrespondenceStatusEntity> Statuses { get; set; }

        public List<CorrespondenceForwardingEventEntity>? ForwardingEvents { get; set; }

        [Required]
        public required DateTimeOffset Created { get; set; }

        public int? Altinn2CorrespondenceId { get; set; }

        public DateTimeOffset? Published { get; set; }

        public bool IsConfirmationNeeded { get; set; }

        public bool IsMigrating { get; set; }
    }
}