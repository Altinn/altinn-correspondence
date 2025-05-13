using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents the content of a reportee element of the type correspondence.
    /// </summary>
    public class CorrespondenceContentExt : InitializeCorrespondenceContentExt
    {
        /// <summary>
        /// Gets or sets a list of attachments.
        /// </summary>
        [JsonPropertyName("attachments")]
        public required new List<CorrespondenceAttachmentExt> Attachments { get; set; }
    }
}