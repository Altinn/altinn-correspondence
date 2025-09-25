using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents a binary attachment to a Correspondence
    /// </summary>
    public class LegacyCorrespondenceAttachmentExt : CorrespondenceAttachmentExt
    {
        /// <summary>
        /// The name of the attachment
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }
}