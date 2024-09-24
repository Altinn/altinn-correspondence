using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Newtonsoft.Json;

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
    }
}