using Altinn.Correspondence.API.Models.Enums;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents the content of a reportee element of the type correspondence.
    /// </summary>
    public class InitializeCorrespondenceContentExt
    {
        /// <summary>
        /// Gets or sets the language that the correspondence is written in.
        /// </summary>
        [JsonPropertyName("language")]
        public LanguageTypeExt Language { get; set; }

        /// <summary>
        /// Gets or sets the correspondence message title. Subject.
        /// </summary>
        /// <remarks>
        /// TODO: Length restriction?
        /// </remarks>
        [JsonPropertyName("messageTitle")]
        public string MessageTitle { get; set; }

        /// <summary>
        /// Gets or sets a summary text of the correspondence.
        /// </summary>
        /// <remarks>
        /// TODO: Length restriction?
        /// </remarks>
        [JsonPropertyName("messageSummary")]
        public string MessageSummary { get; set; }

        /// <summary>
        /// Gets or sets a list of attachments.
        /// </summary>
        /// <remarks>
        /// TODO: Number restriction?
        /// </remarks>
        [JsonPropertyName("attachments")]
        public List<InitializeAttachmentExt> Attachments { get; set; }

        /// <summary>
        /// Ids of the attachments that are to be included in the correspondence.
        /// </summary>
        [JsonPropertyName("attachmentIds")]
        public List<Guid>? AttachmentIds { get; set; }
    }
}