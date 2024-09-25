using System.Text.Json.Serialization;


namespace Altinn.Correspondence.Core.Models.Notifications
{
    public class SmsNotificationWithResult
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        [JsonPropertyName("succeeded")]
        public bool Succeeded { get; set; }

        [JsonPropertyName("recipient")]
        public Recipient Recipient { get; set; } = new();

        [JsonPropertyName("sendStatus")]
        public StatusExt SendStatus { get; set; } = new();
    }
}