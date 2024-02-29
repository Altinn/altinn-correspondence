using System.Runtime.Serialization;
using Altinn.Correspondence.Core.Domain.Models.Enums;

namespace Altinn.Correspondence.Core.Domain.Models
{
    /// <summary>
    /// Notification template details
    /// </summary>
    public class NotificationTemplateBE : ICloneable
    {
        /// <summary>
        /// Gets or sets Identifier for a NotificationTemplate
        /// </summary>
        public int NotificationTemplateID { get; set; }

        /// <summary>
        /// Gets or sets Identifier for a NotificationType
        /// </summary>
        public int NotificationTypeId { get; set; }

        /// <summary>
        /// Gets or sets Name for a NotificationType
        /// </summary>
        public string NotificationName { get; set; }

        /// <summary>
        /// Gets or sets Identifier for FromAddress
        /// </summary>
        public string FromAddress { get; set; }

        /// <summary>
        /// Gets or sets Identifier for Subject
        /// </summary>
        public string Subject { get; set; }

        /// <summary>
        /// Gets or sets Identifier for a NotificationText
        /// </summary>
        public string NotificationText { get; set; }

        /// <summary>
        /// Gets or sets Identifier for a Transport
        /// </summary>
        public TransportType TransportId { get; set; }

        /// <summary>
        /// Gets or sets Identifier for a LanguageType
        /// </summary>
        public LanguageType LanguageTypeID { get; set; }

        /// <summary>
        /// Clones the template
        /// </summary>
        /// <returns>
        /// Clone object
        /// </returns>
        public object Clone()
        {
            NotificationTemplateBE clone = new NotificationTemplateBE();

            clone.NotificationTemplateID = this.NotificationTemplateID;
            clone.NotificationTypeId = this.NotificationTypeId;
            clone.NotificationName = this.NotificationName;
            clone.FromAddress = this.FromAddress;
            clone.Subject = this.Subject;
            clone.NotificationText = this.NotificationText;
            clone.TransportId = this.TransportId;
            clone.LanguageTypeID = this.LanguageTypeID;

            return clone;
        }
    }

    /// <summary>Notification template list</summary>
    public class NotificationTemplateBEList : List<NotificationTemplateBE>
    {
        /// <summary>
        /// Gets or sets a value indicating whether query returned more rows than specified as the maximum number of rows.
        /// </summary>
        public bool LimitReached { get; set; }
    }
}