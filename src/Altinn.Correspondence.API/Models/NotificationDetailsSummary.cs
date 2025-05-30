using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models;

/// <summary>
/// A class representing a summary of status overviews of all notification channels
/// </summary>
public class NotificationStatusDetailsExt
{
    [JsonPropertyName("email")]
    public NotificationDetailsExt? Email { get; set; }

    [JsonPropertyName("sms")]
    public NotificationDetailsExt? Sms { get; set; }

    [JsonPropertyName("emails")]
    public List<NotificationDetailsExt>? Emails { get; set; }

    [JsonPropertyName("smses")]
    public List<NotificationDetailsExt>? Smses { get; set; }
}