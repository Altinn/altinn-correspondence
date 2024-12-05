using Altinn.Correspondence.API.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents an overview of a shared attachment that can be used by multiple correspondences
    /// </summary>
    public class AttachmentDetailsExt : AttachmentOverviewExt
    {
        /// <summary>
        /// The Status history for the Attachment
        /// </summary>
        [JsonPropertyName("statusHistory")]
        public List<AttachmentStatusEvent> StatusHistory { get; set; } = new List<AttachmentStatusEvent>();
    }
}