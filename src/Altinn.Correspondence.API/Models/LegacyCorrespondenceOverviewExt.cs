using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application.GetCorrespondenceOverview;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// An object representing an overview of a correspondence for the legacy API    
    /// </summary>
    public class LegacyCorrespondenceOverviewExt : CorrespondenceOverviewExt
    {
        /// <summary>
        /// The minimum authentication level required to view the correspondence
        /// </summary>
        [JsonPropertyName("minimumAuthenticationLevel")]
        public int MinimumAuthenticationLevel { get; set; }

        /// <summary>
        /// Indicates if the user is authorized to sign the correspondence
        /// </summary>
        [JsonPropertyName("authorizedForSign")]
        public bool AuthorizedForSign { get; set; }

        /// <summary>
        /// The due date for the correspondence
        /// </summary>
        [JsonPropertyName("dueDateTime")]
        public DateTimeOffset? DueDateTime { get; set; }

        /// <summary>
        /// Indicates if the correspondence can be deleted
        /// </summary>
        [JsonPropertyName("allowDelete")]
        public bool AllowDelete { get; set; }

        /// <summary>
        /// The date the correspondence was archived
        /// </summary>
        [JsonPropertyName("archived")]
        public DateTimeOffset? Archived { get; set; }
    }
}