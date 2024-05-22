using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// An entity representing a a list of Correspondences
    /// </summary>
    public class CorrespondencesExt
    {
        /// <summary>
        /// Correspondence ids
        /// </summary>
        [JsonPropertyName("ids")]
        public List<Guid> Ids { get; set; } = new List<Guid>();

        /// <summary>
        /// Metadata for pagination
        /// </summary>
        [JsonPropertyName("pagination")]
        public PaginationMetaDataExt Pagination { get; set; } = new PaginationMetaDataExt();
    }
}
