
namespace Altinn.Correspondence.Core.Models.Enums;

/// <summary>
/// Enum describing available notification channels.
/// </summary>
public enum NotificationChannel
{
    /// <summary>
    /// The selected channel for the notification is email.
    /// </summary>
    Email,

    /// <summary>
    /// The selected channel for the notification is sms.
    /// </summary>
    Sms,

    /// <summary>
    /// The selected channel for the notification is email preferred. 
    /// </summary>
    /// <remarks>
    /// Notification should primarily be sent through email, and SMS should be used if email is not available.
    /// </remarks>
    EmailPreferred,

    /// <summary>
    /// The selected channel for the notification is SMS preferred. 
    /// </summary>
    /// <remarks>
    /// Notification should primarily be sent through SMS, and email should be used if email is not available.
    /// </remarks>
    SmsPreferred
}
