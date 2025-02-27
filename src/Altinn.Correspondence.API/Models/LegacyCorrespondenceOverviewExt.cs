using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application.GetCorrespondenceOverview;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// An object representing an overview of a correspondence for the legacy API    
    /// </summary>
    public class LegacyCorrespondenceOverviewExt : CorrespondenceOverviewExt
    {
        /// <summary>
        /// The minimum authentication level required to view the correspondence
        /// </summary>
        [JsonPropertyName("minimumAuthenticationLevel")]
        public int MinimumAuthenticationLevel { get; set; }

        /// <summary>
        /// Indicates if the user is authorized to sign the correspondence
        /// </summary>
        [JsonPropertyName("authorizedForSign")]
        public bool AuthorizedForSign { get; set; }

        /// <summary>
        /// Indicates if the user is authorized to sign the correspondence
        /// </summary>
        [JsonPropertyName("authorizedForWrite")]
        public bool AuthorizedForWrite { get; set; }

        /// <summary>
        /// The due date for the correspondence
        /// </summary>
        [JsonPropertyName("dueDateTime")]
        public DateTimeOffset? DueDateTime { get; set; }

        /// <summary>
        /// Indicates if the correspondence can be deleted
        /// </summary>
        [JsonPropertyName("allowDelete")]
        public bool AllowDelete { get; set; }

        /// <summary>
        /// The date the correspondence was archived
        /// </summary>
        [JsonPropertyName("archived")]
        public DateTimeOffset? Archived { get; set; }

        /// <summary>
        /// The date the correspondence was confirmed
        /// </summary>
        [JsonPropertyName("confirmationDate")]
        public DateTimeOffset? ConfirmationDate { get; set; }

        /// <summary>
        /// Gets or sets the language of the correspondence, specified according to ISO 639-1 
        /// </summary>
        [JsonPropertyName("language")]
        [ISO6391]
        public required string Language { get; set; }

        /// <summary>
        /// Gets or sets the correspondence message title. Subject.
        /// </summary>
        /// <remarks>
        /// TODO: Length restriction?
        /// </remarks>
        [JsonPropertyName("messageTitle")]
        public required string MessageTitle { get; set; }

        /// <summary>
        /// Gets or sets a summary text of the correspondence.
        /// </summary>
        /// <remarks>
        /// TODO: Length restriction?
        /// </remarks>
        [JsonPropertyName("messageSummary")]
        public required string MessageSummary { get; set; }

        /// <summary>
        /// Gets or sets the main body of the correspondence.
        /// </summary>
        [JsonPropertyName("messageBody")]
        public required string MessageBody { get; set; }

        [JsonPropertyName("attachments")]
        public required new List<LegacyCorrespondenceAttachmentExt> Attachments { get; set; }

        /// <summary>
        /// Instance owner party id
        /// </summary>
        [JsonPropertyName("instanceOwnerPartyId")]
        public int InstanceOwnerPartyId { get; set; }
    }
}