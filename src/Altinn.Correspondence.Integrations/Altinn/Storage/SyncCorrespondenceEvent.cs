using System.Text.Json.Serialization;

namespace Altinn.Correspondence.Integrations.Altinn.Storage
{
    public class SyncCorrespondenceEvent
    {
        /// <summary>
        /// Gets or sets the Altinn 2 ServiceEngine correspondence Id.
        /// </summary>
        [JsonPropertyName("correspondenceId")]
        public int CorrespondenceId { get; set; }

        /// <summary>
        /// Gets or sets the party id of the user causing the event.
        /// </summary>
        [JsonPropertyName("partyId")]
        public int PartyId { get; set; }

        /// <summary>
        /// Gets or sets the timestamp of the event. Timestamp should always be UTC time.
        /// </summary>
        [JsonPropertyName("eventTimestamp")]
        public DateTimeOffset EventTimeStamp { get; set; }

        /// <summary>
        /// Gets or sets the Correspondence Event Type. (Expects Read, Commit or Delete).
        /// </summary>
        [JsonPropertyName("eventType")]
        public string? EventType { get; set; }
    }
}
