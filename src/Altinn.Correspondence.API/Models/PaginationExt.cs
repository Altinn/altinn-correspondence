using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// An entity representing pagination
    /// </summary>
    public class PaginationExt
    {
        /// <summary>
        ///  pagination offset
        /// </summary>
        [JsonPropertyName("offset")]
        public int Offset { get; set; } = 0;

        /// <summary>
        /// pagination limit
        /// </summary>
        [JsonPropertyName("limit")]
        public int Limit { get; set; } = 20;
    }
}
