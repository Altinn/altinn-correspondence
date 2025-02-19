using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models;

/// <summary>
/// An abstract class representing a status overview of a notification channels
/// </summary>
public class NotificationProcessStatusExt
{
    /// <summary>
    /// The actual status of the notification
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// The description of the status
    /// </summary>
    [JsonPropertyName("description")]
    public string? StatusDescription { get; set; }

    /// <summary>
    /// The date time of when the status was last updated
    /// </summary>
    [JsonPropertyName("lastUpdate")]
    public DateTime LastUpdate { get; set; }
}