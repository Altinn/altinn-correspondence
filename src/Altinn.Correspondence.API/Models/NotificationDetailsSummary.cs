namespace Altinn.Correspondence.API.Models;

/// <summary>
/// A class representing a summary of status overviews of all notification channels
/// </summary>
public class NotificationStatusDetailsExt
{
    public List<NotificationDetailsExt>? Email { get; set; }

    public List<NotificationDetailsExt>? Sms { get; set; }
}