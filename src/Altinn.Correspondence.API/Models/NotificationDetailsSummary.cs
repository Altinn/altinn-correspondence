namespace Altinn.Correspondence.API.Models;

/// <summary>
/// A class representing a summary of status overviews of all notification channels
/// </summary>
public class NotificationStatusDetailsExt
{
    public NotificationDetailsExt? Email { get; set; }

    public NotificationDetailsExt? Sms { get; set; }
}