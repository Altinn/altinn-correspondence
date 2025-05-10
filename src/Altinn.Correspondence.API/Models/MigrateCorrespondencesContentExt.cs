using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents the content of a Correspondence.
    /// </summary>
    public class MigrateCorrespondenceContentExt
    {
        /// <summary>
        /// Gets or sets the language of the correspondence, specified according to ISO 639-1 
        /// </summary>
        [JsonPropertyName("language")]
        [ISO6391]
        public required string Language { get; set; }

        /// <summary>
        /// Gets or sets the correspondence message title. Subject.
        /// </summary>
        [JsonPropertyName("messageTitle")]
        public string? MessageTitle { get; set; }

        /// <summary>
        /// Gets or sets a summary text of the correspondence.
        /// </summary>
        [JsonPropertyName("messageSummary")]
        public string? MessageSummary { get; set; }

        /// <summary>
        /// Gets or sets the main body of the correspondence.
        /// </summary>
        public string? MessageBody { get; set; }

        /// <summary>
        /// Gets or sets a list of attachments.
        /// </summary>
        [JsonPropertyName("attachments")]
        public List<InitializeCorrespondenceAttachmentExt> Attachments { get; set; } = new List<InitializeCorrespondenceAttachmentExt>();
    }
}
