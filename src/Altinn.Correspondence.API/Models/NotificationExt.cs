using Altinn.Correspondence.API.Models.Enums;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents a notification with details about a notification.
    /// Notifications are used by agencies to notify end users about new elements that have been created in Altinn for them.
    /// </summary>
    public class NotificationExt
    {
        /// <summary>
        /// Gets or sets a unique id for the notification.
        /// </summary>
        /// <remarks>
        /// Not visible to external systems.
        /// </remarks>
        public Guid NotificationID { get; set; }

        /// <summary>
        /// Gets or sets the date and time for then the notification was created.
        /// </summary>
        /// <remarks>
        /// Not visible to external systems.
        /// </remarks>
        public DateTime CreatedDateTime { get; set; }

        /// <summary>
        /// Gets or sets the sender of the notification. This will set the "from" in SMS and email notifications.
        /// </summary>
        /// <remarks>
        /// Default value is noReply@altinn.no.
        /// </remarks>
        public string FromAddress { get; set; }

        /// <summary>
        /// Gets or sets a date and time for when the notification should be sent to the recipients.
        /// </summary>
        public DateTime RequestedSendTime { get; set; }

        /// <summary>
        /// Gets or sets the unique id of the reportee element this notification is associated with.
        /// </summary>
        /// <remarks>
        /// Not visible to external systems.
        /// </remarks>
        public int ReporteeElementID { get; set; }

        /// <summary>
        /// Gets or sets the language of the notification.
        /// </summary>
        public string LanguageCode { get; set; }

        /// <summary>
        /// Gets or sets the notification type by name. 
        /// </summary>
        public string NotificationType { get; set; }

        /// <summary>
        /// Gets or sets a value to indicate what the notification is about.
        /// </summary>
        public NotificationTypeExternal NotifyType { get; set; }

        /// <summary>
        /// Gets or sets a list of recipients of the notification.
        /// </summary>
        public ReceiverEndPointExternalBEV2List ReceiverEndPoints { get; set; }

        /// <summary>
        /// Gets or sets a list of replacement tokens to insert/replace some content in a notification.
        /// </summary>
        public TextTokenSubstitutionExternalBEList TextTokens { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the registered shortname of the service owner should be used to set the name of the sender for SMS notifications
        /// </summary>
        public bool? UseServiceOwnerShortNameAsSenderOfSms { get; set; }

        /// <summary>
        /// Gets or sets a list of notification templates.
        /// </summary>
        /// <remarks>
        /// Not visible to external systems.
        /// </remarks>
        public NotificationTemplateExternalBEList NotificationTemplateList { get; set; }
    }

    /// <summary>
    /// Represents a strongly typed list of Notification elements that can be accessed by index.
    /// </summary>
    public class NotificationExternalBEV2List : List<NotificationExt>
    {
        /// <summary>
        /// Gets or sets a value indicating whether the query returned more rows than specified as the maximum number of rows.
        /// The user should narrow the search.
        /// </summary>
        public bool LimitReached { get; set; }
    }
}