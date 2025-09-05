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
        /// Gets or sets the UTC timestamp of the event.
        /// </summary>
        [JsonPropertyName("eventTimestamp")]
        public DateTimeOffset EventTimeStamp { get; set; }

        /// <summary>
        /// Gets or sets the Correspondence Event Type. (Expects Read, Confirm, or Delete).
        /// </summary>
        [JsonPropertyName("eventType")]
        public string? EventType { get; set; }
    }
}
