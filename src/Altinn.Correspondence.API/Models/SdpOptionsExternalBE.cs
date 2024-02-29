using System;
using System.Runtime.Serialization;
using Altinn.Correspondence.API.Models.Enums;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents data that are used as input to insert correspondence, to control how it will forward the correspondence to
    /// a secure digital mailbox.
    /// </summary>
    [DataContract(Name = "SdpOptions", Namespace = "http://schemas.altinn.no/services/ServiceEngine/Correspondence/2016/02")]
    public class SdpOptionsExternalBE
    {
        /// <summary>
        /// Gets or sets a value that controls whether Altinn should have a copy of the correspondence.
        /// </summary>
        [DataMember(IsRequired = true)]
        public SdpSettingExternal SdpSetting { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the correspondence should be stored in Altinn instead, if the reportee do
        /// not have a digital mailbox. The default value is "true".
        /// </summary>
        [DataMember]
        public bool? BackupAltinn { get; set; }

        /// <summary>
        /// Gets or sets the name of the file to be used as the primary document in the digital letter. The file name must exist
        /// in the list of attachments in the binary attachments list in the insert correspondence request.
        /// </summary>
        [DataMember(IsRequired = true)]
        public string PrimaryDocumentFileName { get; set; }

        /// <summary>
        /// Gets or sets the notification settings for the digital letter. This is not Altinn notifications, but notifications
        /// via the mailbox supplier.
        /// </summary>
        [DataMember]
        public SdpNotificationsExternalBE SdpNotifications { get; set; }

        /// <summary>
        /// Gets or sets the Message title used on the digital letter send via the mailbox provider
        /// </summary>
        [DataMember]
        public string Title { get; set; }

        /// <summary>
        /// Gets or sets the Non sensitive message title used on the digital letter send via the mailbox provider
        /// </summary>
        [DataMember]
        public string NotSensitiveTitle { get; set; }

        /// <summary>
        /// Gets or sets the Visible date time used on the digital letter send via the mailbox provider
        /// </summary>
        [DataMember]
        public DateTime? VisibleDateTime { get; set; }
    }
}
