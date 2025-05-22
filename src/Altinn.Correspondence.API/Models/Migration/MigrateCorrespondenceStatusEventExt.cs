using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models.Migration
{
    public class MigrateCorrespondenceStatusEventExt : CorrespondenceStatusEventExt
    {
        [JsonPropertyName("eventUserUuid")]
        public Guid? EventUserUuid { get; set; }

        [JsonPropertyName("eventUserPartyUuid")]
        public required Guid EventUserPartyUuid { get; set; }
    }
}