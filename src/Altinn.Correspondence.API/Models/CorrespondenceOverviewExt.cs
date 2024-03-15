﻿using Altinn.Correspondence.API.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// An object representing an overview of a correspondence with enough details to drive the business process    
    /// </summary>
    public class CorrespondenceOverviewExt
    {
        /// <summary>
        /// Unique Id for this correspondence
        /// </summary>
         [JsonPropertyName("correspondenceId")]
        public required Guid CorrespondenceId { get; set; }

        // <summary>
        /// Gets or sets the Resource Id for the correspondence service.
        /// </summary>
        [JsonPropertyName("resourceId")]
        [StringLength(255, MinimumLength = 1)]
        [Required]
        public required string ResourceId { get; set; }

        /// <summary>
        /// The recipient of the correspondence, either an organisation or an person
        /// </summary>
        /// <remarks>
        /// National identity number or Organization number.
        /// </remarks
        [JsonPropertyName("recipient")]
        [Required]
        public required string Recipient { get; set; }

        /// <summary>
        /// The Sending organisation of the correspondence. 
        /// </summary>
        /// <remarks>
        /// Organization number in countrycode:organizationnumber format.
        /// </remarks>
        [JsonPropertyName("sender")]
        [RegularExpression(@"^\d{4}:\d{9}$", ErrorMessage = "Organization numbers should be on the form countrycode:organizationnumber, for instance 0192:910753614")]
        [Required]
        public required string Sender { get; set; }

        /// <summary>
        /// Used by senders and receivers to identify specific a Correspondence using external identification methods.
        /// </summary>
        [JsonPropertyName("sendersReference")]
        [StringLength(4096, MinimumLength = 1)]
        [Required]
        public required string SendersReference { get; set; }

        /// <summary>
        /// The correspondence content. Contains information about the Correspondence body, subject etc.
        /// </summary>
        [JsonPropertyName("content")]
        public required CorrespondenceContentExt Content { get; set; }

        /// <summary>
        /// When the correspondence was created
        /// </summary>
        [JsonPropertyName("createdDateTime")]
        public required DateTime CreatedDateTime { get; set; }

        /// <summary>
        /// When the correspondence should become visible to the recipient.
        /// </summary>
        [JsonPropertyName("visibleDateTime")]
        public required DateTime VisibleDateTime { get; set; }

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
        /// User-defined properties related to the Correspondence
        /// </summary>
        [JsonPropertyName("propertyList")]
        [MaxLength(10, ErrorMessage = "propertyList can contain at most 10 properties")]
        public Dictionary<string, string> PropertyList { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Options for how the recipient can reply to the correspondence
        /// </summary>
        [JsonPropertyName("replyOptions")]
        public List<CorrespondenceReplyOptionExt>? ReplyOptions { get; set; }

        /// <summary>
        /// Specifies whether the correspondence can override reservation against digital comminication in KRR
        /// </summary>
        [JsonPropertyName("isReservable")]
        public bool? IsReservable { get; set; }

        /// <summary>
        /// The current status for the Correspondence
        /// </summary>
        public CorrespondenceStatusExt Status { get; set; }
        
        /// <summary>
        /// The current status text for the Correspondence
        /// </summary>
        public string StatusText { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp for when the Current Correspondence Status was changed
        /// </summary>
        public DateTimeOffset StatusChanged { get; set; }
    }
}
