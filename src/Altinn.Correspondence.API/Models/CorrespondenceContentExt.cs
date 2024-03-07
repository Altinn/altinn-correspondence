using Altinn.Correspondence.API.Models.Enums;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents the content of a reportee element of the type correspondence.
    /// </summary>
    public class CorrespondenceContentExt
    {
        /// <summary>
        /// Gets or sets the language that the correspondence is written in.
        /// </summary>
        public LanguageTypeExt Language { get; set; }

        /// <summary>
        /// Gets or sets the correspondence message title. Subject.
        /// </summary>
        public string MessageTitle { get; set; }

        /// <summary>
        /// Gets or sets a summary text of the correspondence.
        /// </summary>
        public string MessageSummary { get; set; }

        /// <summary>
        /// Gets or sets the main message body in a correspondence.
        /// </summary>
        public string MessageBody { get; set; }

        /// <summary>
        /// Gets or sets a list of attachments.
        /// </summary>
        public List<CorrespondenceAttachmentExt> Attachments { get; set; }

        /// Gets or sets custom xml content.
        /// </summary>]
        public string CustomMessageData { get; set; }
    }
}