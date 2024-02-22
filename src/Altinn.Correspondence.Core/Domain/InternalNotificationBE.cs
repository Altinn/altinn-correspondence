using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Core.Models
{
    /// <summary>
    /// The NotificationBE entity contains information
    /// related Notifications sent to a Correspondence.
    /// </summary>
    public class InternalNotificationBE
    {
        /// <summary>
        /// Gets or sets Identifier for a Notification
        /// </summary>
        public int NotificationID { get; set; }

        /// <summary>
        /// Gets or sets DateTime of creation of the notification
        /// </summary>
        public DateTime CreatedDateTime { get; set; }

        /// <summary>
        /// Gets or sets sending date time of notification
        /// </summary>
        public DateTime ShipmentDateTime { get; set; }

        /// <summary>
        /// Gets or sets the receiver of a Notification
        /// </summary>
        public int ReporteeElementID { get; set; }

        /// <summary>
        /// Gets or sets the receiver of a Notification
        /// </summary>
        public string LanguageCode { get; set; }

        /// <summary>
        /// Gets or sets Default sender used when no sender is necessary as for SMS and Emails
        /// when the address is only set to noReply@altinn.no
        /// </summary>
        public string FromAddress { get; set; }

        /// <summary>
        /// Gets or sets the Notification Type
        /// </summary>
        public string NotificationTypeName { get; set; }

        /// <summary>
        /// Gets or sets the Notification Type
        /// </summary>
        public NotificationType NotifyType { get; set; }

        /// <summary>
        /// Gets or sets Receiver End Point List
        /// </summary>
        public InternalReceiverEndPointBEList ReceiverEndPointList { get; set; }

        /// <summary>
        /// Gets or sets Text Token Substitution List
        /// </summary>
        public TextTokenSubstitutionBEList TextTokenSubstitutionList { get; set; }

        /// <summary>
        /// Gets or sets Notification Template List
        /// </summary>
        public NotificationTemplateBEList NotificationTemplateList { get; set; }
    }

    /// <summary>Internal notification list</summary>
    public class InternalNotificationBEList : List<InternalNotificationBE>
    {
        /// <summary>
        /// Gets or sets a value indicating whether the query returned more rows than specified as the maximum number of rows.
        /// </summary>
        public bool LimitReached { get; set; }
    }
}