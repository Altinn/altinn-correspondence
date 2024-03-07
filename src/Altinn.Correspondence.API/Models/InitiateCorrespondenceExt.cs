using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents a request object for the operation, InsertCorrespondence, that can create a correspondence in Altinn.
    /// Instances of this class can hold the complete set of information about a correspondence. 
    /// </summary>
    public class InitiateCorrespondenceExt
    {
        /// <summary>
        /// Gets or sets the Resource Id for the correspondence service.
        /// </summary>
        [JsonPropertyName("resourceId")]
        [StringLength(255, MinimumLength = 1)]
        [Required]
        public string ResourceId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the recipient of the correspondence. 
        /// </summary>
        /// <remarks>
        /// Social Security number or Organization number.
        /// TODO: How to validate?
        /// </remarks
        [JsonPropertyName("recipient")]
        [Required]
        public string Recipient { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Sender of the correspondence. 
        /// </summary>
        /// <remarks>
        /// Organization number.
        /// </remarks>
        [JsonPropertyName("sender")]
        [RegularExpression(@"^\d{4}:\d{9}$", ErrorMessage = "Organization numbers should be on the form countrycode:organizationnumber, for instance 0192:910753614")]
        [Required]
        public string Sender { get; set; } = string.Empty;

        /// <summary>
        /// Used by senders and receivers to identify specific a Correspondence using external identification methods.
        /// </summary>
        [JsonPropertyName("sendersReference")]
        [StringLength(4096, MinimumLength = 1)]
        [Required]
        public required string SendersReference { get; set; }

        /// <summary>
        /// Gets or sets the correspondence content. Contains information about the Correspondence body, subject etc.
        /// </summary>
        [JsonPropertyName("content")]
        public required CorrespondenceContentExt Content { get; set; }

        /// <summary>
        /// Gets or sets when the correspondence should become visible to the recipient.
        /// </summary>
        [JsonPropertyName("visibleDateTime")]
        public DateTime VisibleDateTime { get; set; }

        /// <summary>
        /// Gets or sets the date for when Altinn can remove the correspondence from its database.
        /// </summary>
        [JsonPropertyName("allowSystemDeleteDateTime")]
        public DateTime? AllowSystemDeleteDateTime { get; set; }

        /// <summary>
        /// Gets or sets a date and time for when the recipient must reply.
        /// </summary>
        [JsonPropertyName("dueDateTime")]
        public DateTime DueDateTime { get; set; }

        /// <summary>
        /// Gets or sets an list of references Senders can use this field to tell the recipient that the correspondence is related to the referenced item(s)
        /// Examples include Altinn App instances, Altinn Broker File Transfers
        /// </summary>
        [JsonPropertyName("externalReferences")]
        public List<ExternalReferenceExt>? ExternalReferences { get; set; }

        /// <summary>
        /// Gets or sets options for how the recipient can reply to the correspondence
        /// </summary>
        public List<CorrespondenceReplyOptionExt>? ReplyOptions { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the service could be reserved or not
        /// </summary>
        public bool? IsReservable { get; set; }
    }
}