using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models.Migration
{
    public class MigrateCorrespondenceReplyOptionExt
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
