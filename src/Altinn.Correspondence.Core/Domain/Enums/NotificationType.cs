namespace Altinn.Correspondence.Core.Domain.Models.Enums
{
    /// <summary>
    /// This specifies what type of notification to be used
    /// </summary>
    public enum NotificationType : int
    {
        /// <summary>
        /// The Notify service should be used for PIN notification
        /// </summary>
        PIN = 1, 

        /// <summary>
        /// The Notify service should be used for Correspondence
        /// </summary>
        Correspondence = 2, 

        /// <summary>
        /// This Notification can be used at the time of PreFill
        /// </summary>
        PreFill = 3, 

        /// <summary>
        /// This Notification can be used at the time of Standalone notification
        /// </summary>
        StandAlone = 5, 

        /// <summary>
        /// This Type is used when Notification has to send for other cases apart from PIN,Correspondence, PreFill and Standalone.
        /// </summary>
        General = 4
    }
}