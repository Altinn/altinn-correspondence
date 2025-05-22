using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models.Migration
{
    public class MigrateInitializeCorrespondenceContentExt
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
        public required string MessageTitle { get; set; }

        /// <summary>
        /// Gets or sets a summary text of the correspondence.
        /// </summary>
        [JsonPropertyName("messageSummary")]
        public required string MessageSummary { get; set; }

        /// <summary>
        /// Gets or sets the main body of the correspondence.
        /// </summary>
        public required string MessageBody { get; set; }

        /// <summary>
        /// Gets or sets a list of attachments.
        /// </summary>
        /// <remarks>
        /// Maximum of 100 attachments allowed.
        /// </remarks>
        [JsonPropertyName("attachments")]
        [MaxListCount(100, "attachments")]
        public List<InitializeCorrespondenceAttachmentExt> Attachments { get; set; } = new List<InitializeCorrespondenceAttachmentExt>();
    }
}
