using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    public class MigrateCorrespondenceExt
    {
        [JsonPropertyName("correspondenceData")]
        public required InitializeCorrespondencesExt CorrespondenceData { get; set; }
        
        [JsonPropertyName("altinn2CorrespondenceId")]
        public required int Altinn2CorrespondenceId { get; set; }
        
        [JsonPropertyName("eventHistory")]
        public List<CorrespondenceStatusEventExt> EventHistory { get; set; } = new List<CorrespondenceStatusEventExt>();

        [JsonPropertyName("notificationHistory")]
        public List<MigrateCorrespondenceNotificationExt> NotificationHistory { get; set; } = new List<MigrateCorrespondenceNotificationExt>();

        [JsonPropertyName("forwardingHistory")]
        public List<MigrateCorrespondenceForwardingEventExt> ForwardingHistory { get; set; } = new List<MigrateCorrespondenceForwardingEventExt>();

        [JsonPropertyName("IsMigrating")]
        public bool IsMigrating { get; set; }
    }
}