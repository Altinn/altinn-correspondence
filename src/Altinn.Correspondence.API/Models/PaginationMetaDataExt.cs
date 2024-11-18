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
        [JsonPropertyName("totalItems")]
        public int TotalItems { get; set; } = 0;

        /// <summary>
        /// The given page number
        /// </summary>
        [JsonPropertyName("page")]
        public int Page { get; set; } = 1;

        /// <summary>
        /// The number of pages
        /// </summary>
        [JsonPropertyName("pages")]
        public int TotalPages { get; set; } = 1;
    }
}
