using System.Text.Json.Serialization;

namespace Altinn.Correspondence.Core.Models.Notifications;

public class NotificationResourceLinks
{
    [JsonPropertyName("self")]
    public string Self { get; set; } = string.Empty;
}