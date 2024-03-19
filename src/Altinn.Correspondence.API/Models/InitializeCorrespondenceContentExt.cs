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
        /// Gets or sets the main message body in a correspondence.
        /// </summary>
        /// <remarks>
        /// TODO: Maybe the messagebody should simply be another "attachment"?
        /// TODO: Length restriction?
        /// DECISION: Clarfy external requirments
        /// </remarks>
        [JsonPropertyName("messageBody")]
        public string MessageBody { get; set; }

        /// <summary>
        /// Gets or sets a list of attachments.
        /// </summary>
        /// <remarks>
        /// TODO: Number restriction?
        /// </remarks>
        [JsonPropertyName("attachments")]
        public List<InitializeCorrespondenceAttachmentExt> Attachments { get; set; }
    }
}