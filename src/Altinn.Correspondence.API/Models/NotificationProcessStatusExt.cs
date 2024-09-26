using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models;

/// <summary>
/// An abstrct  class representing a status overview of a notification channels
/// </summary>
public class NotificationProcessStatusExt
{
    /// <summary>
    /// Gets or sets the status
    /// </summary>
    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the description
    /// </summary>
    [JsonPropertyName("description")]
    public string? StatusDescription { get; set; }

    /// <summary>
    /// Gets or sets the date time of when the status was last updated
    /// </summary>
    [JsonPropertyName("lastUpdate")]
    public DateTime LastUpdate { get; set; }
}