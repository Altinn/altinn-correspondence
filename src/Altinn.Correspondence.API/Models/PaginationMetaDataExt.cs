using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// An entity representing a list of Correspondences
    /// </summary>
    public class PaginationMetaDataExt
    {
        /// <summary>
        /// Total number of Correspondences
        /// </summary>
        [JsonPropertyName("TotalItems")]
        public int TotalItems { get; set; } = 0;

        /// <summary>
        /// The given page number
        /// </summary>
        [JsonPropertyName("Page")]
        public int Page { get; set; } = 1;

        /// <summary>
        /// The number of pages
        /// </summary>
        [JsonPropertyName("Pages")]
        public int TotalPages { get; set; } = 1;
    }
}
