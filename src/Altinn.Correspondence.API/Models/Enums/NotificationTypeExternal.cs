namespace Altinn.Correspondence.API.Models.Enums
{
    /// <summary>
    /// Defines what type of notification to be used.
    /// </summary>
    public enum NotificationTypeExternal : int
    {
        /// <summary>
        /// Specifies that the notification service should be used for PIN notification.
        /// </summary>
        PIN = 1,

        /// <summary>
        /// Specifies that the notification service should be used for correspondence.
        /// </summary>
        Correspondence = 2,

        /// <summary>
        /// Specifies that the the notification service can be used at the time of PreFill.
        /// </summary>
        PreFill = 3,

        /// <summary>
        /// Specifies when notification service has to send for other cases than PIN, correspondence or prefill.
        /// </summary>
        General = 4
    }
}