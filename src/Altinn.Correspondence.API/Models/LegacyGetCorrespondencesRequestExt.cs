using Altinn.Correspondence.API.Models.Enums;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Request for legacy correspondence search 
    /// </summary>
    public class LegacyGetCorrespondencesRequestExt
    {
        /// <summary>
        /// Pagination offset
        /// </summary>
        [JsonPropertyName("offset")]
        public int Offset { get; set; }

        /// <summary>
        /// Pagination limit
        /// </summary>
        [JsonPropertyName("limit")]
        public int Limit { get; set; }

        /// <summary>
        /// PartyId of the end user performing the search
        /// </summary>
        [JsonPropertyName("onbehalfOfPartyId")]
        public required int OnbehalfOfPartyId { get; set; }

        /// <summary>
        /// A list of the parties/recipients that own the Correspondence instances
        /// </summary>
        [JsonPropertyName("instanceOwnerPartyIdList")]
        public required int[] InstanceOwnerPartyIdList { get; set; }

        /// <summary>
        /// If the search should include active correspondences
        /// </summary>
        [JsonPropertyName("includeActive")]
        public bool IncludeActive { get; set; }

        /// <summary>
        /// If the search should include archived correspondences
        /// </summary>
        [JsonPropertyName("includeArchived")]
        public bool IncludeArchived { get; set; }

        /// <summary>
        /// If the search should include deleted correspondences
        /// </summary>
        [JsonPropertyName("includeDeleted")]
        public bool IncludeDeleted { get; set; }

        /// <summary>
        /// If the seach should filter by published date - from
        /// </summary>
        [JsonPropertyName("from")]
        public DateTimeOffset? From { get; set; }

        /// <summary>
        /// If the seach should filter by published date - to
        /// </summary>
        [JsonPropertyName("to")]
        public DateTimeOffset? To { get; set; }

        /// <summary>
        /// Search string for MessageTitle
        /// </summary>
        [JsonPropertyName("searchString")]
        public string? SearchString { get; set; }

        /// <summary>
        /// Language to filter by
        /// </summary>
        [JsonPropertyName("language")]
        [ISO6391]
        public string? Language { get; set; }

        /// <summary>
        /// Specific status to filter by
        /// </summary>
        [JsonPropertyName("status")]
        public CorrespondenceStatusExt? Status { get; set; }
    }
}