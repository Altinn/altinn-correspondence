using System.Text.Json.Serialization;

namespace Altinn.Correspondence.Core.Models.Notifications;

public class StatusExt
{
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string? StatusDescription { get; set; }

    [JsonPropertyName("lastUpdate")]
    public DateTime LastUpdate { get; set; }
}