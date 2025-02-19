using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models;

/// <summary>
/// A class representing a status summary
/// </summary>
/// <remarks>
/// External representation to be used in the API.
/// </remarks>
public class NotificationStatusExt
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