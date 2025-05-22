using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Altinn.Correspondence.Common.Constants;

namespace Altinn.Correspondence.API.Models.Migration
{
    /// <summary>
    /// Represents a request object for the operation, InitializeCorrespondence, that can create a correspondence in Altinn.    
    /// </summary>
    public class MigrateBaseCorrespondenceExt
    {
        /// <summary>
        /// The Resource Id associated with the correspondence service.
        /// </summary>
        [JsonPropertyName("resourceId")]
        [StringLength(255, MinimumLength = 1)]
        [Required]
        public required string ResourceId { get; set; }

        /// <summary>
        /// The Sending organization of the correspondence. 
        /// </summary>
        /// <remarks>
        /// Organization number must be formatted as countrycode:organizationnumber.
        /// </remarks>
        [JsonPropertyName("sender")]
        [OrganizationNumber(ErrorMessage = $"Organization numbers should be on the format '{UrnConstants.OrganizationNumberAttribute}:organizationnumber' or the format countrycode:organizationnumber, for instance 0192:910753614")]
        [Required]
        public required string Sender { get; set; }

        /// <summary>
        /// A reference used by senders and receivers to identify a specific Correspondence using external identification methods.
        /// </summary>
        [JsonPropertyName("sendersReference")]
        [StringLength(4096, MinimumLength = 1)]
        [Required]
        public required string SendersReference { get; set; }

        /// <summary>
        /// An alternative name for the sender of the correspondence. The name will be displayed instead of the organization name.
        ///  </summary>
        [JsonPropertyName("messageSender")]
        [StringLength(256, MinimumLength = 0)]
        public string? MessageSender { get; set; }

        /// <summary>
        /// The correspondence content. Contains information about the Correspondence body, subject etc.
        /// </summary>
        [JsonPropertyName("content")]
        public MigrateInitializeCorrespondenceContentExt? Content { get; set; }

        /// <summary>
        /// When the correspondence should become visible to the recipient.
        /// </summary>
        [JsonPropertyName("requestedPublishTime")]
        public DateTimeOffset? RequestedPublishTime { get; set; }

        /// <summary>
        /// When Altinn can remove the correspondence from its database.
        /// </summary>
        [JsonPropertyName("allowSystemDeleteAfter")]
        public DateTimeOffset? AllowSystemDeleteAfter { get; set; }

        /// <summary>
        /// When the recipient must reply to the correspondence
        /// </summary>
        [JsonPropertyName("dueDateTime")]
        public DateTimeOffset? DueDateTime { get; set; }

        /// <summary>
        /// A list of references Senders can use to tell the recipient that the correspondence is related to the referenced item(s)
        /// Examples include Altinn App instances, Altinn Broker File Transfers
        /// </summary>
        [JsonPropertyName("externalReferences")]
        [ExternalReferences]
        public List<MigrateExternalReferenceExt>? ExternalReferences { get; set; }

        /// <summary>
        /// User-defined properties related to the Correspondence
        /// </summary>
        [JsonPropertyName("propertyList")]
        [PropertyList]
        public Dictionary<string, string> PropertyList { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Options for how the recipient can reply to the Correspondence
        /// </summary>
        [JsonPropertyName("replyOptions")]
        public List<MigrateCorrespondenceReplyOptionExt>? ReplyOptions { get; set; } = new List<MigrateCorrespondenceReplyOptionExt>();

        /// <summary>
        /// Notifications related to the Correspondence.
        /// </summary>
        [JsonPropertyName("notification")]
        public MigrateInitializeCorrespondenceNotificationExt? Notification { get; set; }

        /// <summary>
        /// Specifies whether the correspondence can override reservation against digital communication in KRR
        /// </summary>
        [JsonPropertyName("ignoreReservation")]
        public bool? IgnoreReservation { get; set; }

        /// <summary>
        /// Is null until the correspondence is published.
        /// </summary>
        [JsonPropertyName("published")]
        public DateTimeOffset? Published { get; set; }

        /// <summary>
        /// Specifies whether reading the correspondence needs to be confirmed by the recipient
        /// </summary>
        [JsonPropertyName("isConfirmationNeeded")]
        public bool IsConfirmationNeeded { get; set; }
    }
}
