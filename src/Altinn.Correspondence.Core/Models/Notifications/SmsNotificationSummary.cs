using System.Text.Json.Serialization;

namespace Altinn.Correspondence.Core.Models.Notifications
{
    public class SmsNotificationSummary
    {
        [JsonPropertyName("orderId")]
        public Guid OrderId { get; set; }

        [JsonPropertyName("sendersReference")]
        public string? SendersReference { get; set; }

        [JsonPropertyName("generated")]
        public int Generated { get; set; }

        [JsonPropertyName("succeeded")]
        public int Succeeded { get; set; }

        [JsonPropertyName("notifications")]
        public List<SmsNotificationWithResult> Notifications { get; set; } = [];
    }
}