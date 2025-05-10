using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    public class MigrateInitializeAttachmentExt : InitializeAttachmentExt
    {
        [JsonPropertyName("senderPartyUuid")]
        public required Guid SenderPartyUuid { get; set; }
    }
}