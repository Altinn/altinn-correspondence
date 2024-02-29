using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents a request object for the operation, InsertCorrespondence, that can create a correspondence in Altinn.
    /// Instances of this class can hold the complete set of information about a correspondence. 
    /// </summary>
    public class InsertCorrespondenceExt
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
        public string SendersReference { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the correspondence content. Contains information about the Correspondence body, subject etc.
        /// </summary>
        public required CorrespondenceContentExt Content { get; set; }

        /// <summary>
        /// Gets or sets when the correspondence should become visible to the recipient.
        /// </summary>
        public DateTime VisibleDateTime { get; set; }

        /// <summary>
        /// Gets or sets the date for when Altinn can remove the correspondence from its database.
        /// </summary>
        public DateTime? AllowSystemDeleteDateTime { get; set; }

        /// <summary>
        /// Gets or sets a date and time for when the recipient must reply.
        /// </summary>
        public DateTime DueDateTime { get; set; }

        /// <summary>
        /// Gets or sets an list of references Senders can use this field to tell the recipient that the correspondence is related to the referenced item(s)
        /// Examples include Altinn 2 FormTask submissions, Altinn 2 CaseId, Altinn App instances, Altinn Broker File Transfers
        /// </summary>
        public List<ReferenceExt>? ExternalReferences { get; set; }

        /// <summary>
        /// Gets or sets options for how the recipient can reply to the correspondence
        /// </summary>
        public CorrespondenceInsertLinkExternalBEList? ReplyOptions { get; set; }

        /// <summary>
        /// Gets or sets notification information. Notifications are used to inform the recipient that there is a new correspondence.
        /// </summary>
        public NotificationExternalBEV2List? Notifications { get; set; }

        /// <summary>
        /// Gets or sets a flag that indicate whether the user is allowed to forward the correspondence.
        /// </summary>
        public bool? AllowForwarding { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the service could be reserved or not
        /// </summary>
        public bool? IsReservable { get; set; }

        /// <summary>
        /// Gets or sets a data object containing details about how Altinn should work when making a submission
        /// to the digital mailbox system.
        /// </summary>
        public SdpOptionsExternalBE SdpOptions { get; set; }
    }
}