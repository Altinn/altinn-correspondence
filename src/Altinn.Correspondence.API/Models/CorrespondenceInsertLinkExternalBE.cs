using Altinn.Correspondence.API.Models.Enums;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents a ReplyOption with information provided by a service owner. A reply option is a way for recipients to respond to
    /// a correspondence.
    /// </summary>
    public class CorrespondenceInsertLinkExternalBE
    {
        /// <summary>
        /// Gets or sets service details. Respond to a correspondence by filling out a form.
        /// </summary>
        public InsertCorrespondenceLinkServiceCodeExternalBE Service { get; set; }

        /// <summary>
        /// Gets or sets an associated archive reference.
        /// </summary>
        public InsertCorrespondenceLinkArchiveRefExternalBE ArchiveReference { get; set; }

        /// <summary>
        /// Gets or sets link information. Respond by following a link.
        /// </summary>
        public InsertCorrespondenceLinkServiceURLExternalBE URL { get; set; }

        /// <summary>
        /// Gets or sets the type or reply this is.
        /// </summary>
        public ReplyOptionTypeExternal ReplyOptionType { get; set; }
    }

    /// <summary>
    /// Represents a strongly typed list of ReplyOption elements that can be accessed by index.
    /// </summary>
    public class CorrespondenceInsertLinkExternalBEList : List<CorrespondenceInsertLinkExternalBE>
    {
        /// <summary>
        /// Gets or sets a value indicating whether the query returned more rows than specified as the maximum number of rows.
        /// The user should narrow the search.
        /// </summary>
        public bool LimitReached { get; set; }
    }
}