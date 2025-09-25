using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents a ReplyOption with information provided by the sender.
    /// A reply option is a way for recipients to respond to a correspondence in addition to the normal Read and Confirm operations
    /// </summary>
    public class CorrespondenceReplyOptionExt
    {
        /// <summary>
        /// Gets or sets the URL to be used as a reply/response to a correspondence. 
        /// </summary>
        [JsonPropertyName("linkURL")]
        public required string LinkURL { get; set; }

        /// <summary>
        /// Gets or sets the url text.
        /// </summary>
        [JsonPropertyName("linkText")]
        public string? LinkText { get; set; }
    }
}