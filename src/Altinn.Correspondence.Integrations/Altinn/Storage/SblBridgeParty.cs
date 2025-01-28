using System.Text.Json.Serialization;

namespace Altinn.Correspondence.Integrations.Altinn.Storage
{
    public class SblBridgeParty
    {
        [JsonPropertyName("partyId")]
        public int PartyId { get; set; }
    }
}
