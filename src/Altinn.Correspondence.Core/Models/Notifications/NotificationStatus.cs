using System.Text.Json.Serialization;

namespace Altinn.Correspondence.Core.Models.Notifications;

/// <summary>
/// An abstrct  class representing a status overview of a notification channels
/// </summary>
public abstract class NotificationStatus
{
    /// <summary>
    /// Gets or sets the self link of the notification status object
    /// </summary>
    [JsonPropertyName("links")]
    public NotificationResourceLinks Links { get; set; } = new();

    /// <summary>
    /// Gets or sets the number of generated notifications
    /// </summary>    
    [JsonPropertyName("generated")]
    public int Generated { get; set; }

    /// <summary>
    /// Gets or sets the number of succeeeded notifications
    /// </summary>
    [JsonPropertyName("succeeded")]
    public int Succeeded { get; set; }
}

/// <summary>
/// A class representing a status overview for email notifications 
/// </summary>
/// <remarks>
/// External representaion to be used in the API.
/// </remarks>
public class EmailNotificationStatusExt : NotificationStatus
{
}

/// <summary>
/// A class representing a status overview for sms notifications 
/// </summary>
/// <remarks>
/// External representaion to be used in the API.
/// </remarks>
public class SmsNotificationStatusExt : NotificationStatus
{
}