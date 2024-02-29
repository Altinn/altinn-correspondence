namespace Altinn.Correspondence.Core.Domain.Models.Enums
{
    /// <summary>
    /// Defines types of transportation for sending notifications.
    /// </summary>
    public enum TransportTypeExternal
    {
        /// <summary>
        /// Specifies that notifications should be sent via SMS.
        /// </summary>
        SMS = 1, 

        /// <summary>
        /// Specifies that notifications should be sent via email.
        /// </summary>
        Email = 2, 

        /// <summary>
        /// Specifies that notifications should be sent via IM.
        /// </summary>
        IM = 3, 

        /// <summary>
        /// Specifies that notifications should be sent via both SMS and email.
        /// </summary>
        Both = 4,

        /// <summary>
        /// Specifies that notifications is preferred to be sent via SMS, but should be sent by email if no phone number exist.
        /// </summary>
        SMSPreferred = 5,

        /// <summary>
        ///  Specifies that notifications is preferred to be sent via Email, but should be sent by SMS, of no email address exist.
        /// </summary>
        EmailPreferred = 6
    }
}