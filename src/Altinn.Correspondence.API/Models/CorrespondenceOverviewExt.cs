using Altinn.Correspondence.API.Models.Enums;
using Altinn.Correspondence.Application.GetCorrespondenceOverview;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// An object representing an overview of a correspondence with enough details to drive the business process    
    /// </summary>
    public class CorrespondenceOverviewExt : BaseCorrespondenceExt
    {
        /// <summary>
        /// The recipient of the correspondence.
        /// </summary>
        [JsonPropertyName("recipient")]
        public required string Recipient { get; set; }

        /// <summary>
        /// Indicates if the Correspondence has been set as unread by the recipient
        /// </summary>
        [JsonPropertyName("markedUnread")]
        public bool? MarkedUnread { get; set; }

        /// <summary>
        /// Unique Id for this correspondence
        /// </summary>
        [JsonPropertyName("correspondenceId")]
        public required Guid CorrespondenceId { get; set; }

        /// <summary>
        /// The correspondence content. Contains information about the Correspondence body, subject etc.
        /// </summary>
        [JsonPropertyName("content")]
        public new CorrespondenceContentExt? Content { get; set; }

        /// <summary>
        /// When the correspondence was created
        /// </summary>
        [JsonPropertyName("created")]
        public required DateTimeOffset Created { get; set; }

        /// <summary>
        /// The current status for the Correspondence
        /// </summary>
        [JsonPropertyName("status")]
        public CorrespondenceStatusExt Status { get; set; }

        /// <summary>
        /// The current status text for the Correspondence
        /// </summary>
        [JsonPropertyName("statusText")]
        public string StatusText { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp for when the Current Correspondence Status was changed
        /// </summary>
        [JsonPropertyName("statusChanged")]
        public DateTimeOffset StatusChanged { get; set; }

        /// <summary>
        /// An overview of the notifications for this correspondence
        /// </summary>
        [JsonPropertyName("notifications")]
        public List<CorrespondenceNotificationOverview> Notifications { get; set; } = new List<CorrespondenceNotificationOverview>();

        /// <summary>
        /// Is null until the correspondence is published.
        /// </summary>
        [JsonPropertyName("Published")]
        public DateTimeOffset? Published { get; set; }
    }
}
