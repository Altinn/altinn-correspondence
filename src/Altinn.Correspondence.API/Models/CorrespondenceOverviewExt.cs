using Altinn.Correspondence.API.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// An object representing an overview of a correspondence with enough details to drive the business process    
    /// </summary>
    public class CorrespondenceOverviewExt : InitializeCorrespondenceExt
    {
        /// <summary>
        /// Unique Id for this correspondence
        /// </summary>
         [JsonPropertyName("correspondenceId")]
        public required Guid CorrespondenceId { get; set; }

        /// <summary>
        /// The correspondence content. Contains information about the Correspondence body, subject etc.
        /// </summary>
        [JsonPropertyName("content")]
        public new CorrespondenceContentExt Content { get; set; }

        /// <summary>
        /// When the correspondence was created
        /// </summary>
        [JsonPropertyName("createdDateTime")]
        public required DateTimeOffset CreatedDateTime { get; set; }

        /// <summary>
        /// The current status for the Correspondence
        /// </summary>
        public CorrespondenceStatusExt Status { get; set; }
        
        /// <summary>
        /// The current status text for the Correspondence
        /// </summary>
        public string StatusText { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp for when the Current Correspondence Status was changed
        /// </summary>
        public DateTimeOffset StatusChanged { get; set; }
    }
}
