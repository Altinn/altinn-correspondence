using System.Text.Json.Serialization;

namespace Altinn.Correspondence.Core.Models.Notifications;

public abstract class NotificationStatus
{

    [JsonPropertyName("links")]
    public NotificationResourceLinks Links { get; set; } = new();


    [JsonPropertyName("generated")]
    public int Generated { get; set; }

    [JsonPropertyName("succeeded")]
    public int Succeeded { get; set; }
}

public class EmailNotificationStatus : NotificationStatus
{
}

public class SmsNotificationStatus : NotificationStatus
{
}