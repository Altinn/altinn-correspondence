using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Core.Models
{
    /// <summary>
    /// The CorrespondenceLinkBE provides Correspondence link information provided by a Service Owner
    /// Each of the properties listed below has a one-on-one mapping with a column either from ReplyOption or
    /// ReplyOptionType or ServiceEdition or Service table in the Service Engine DB.
    /// </summary>
    public class CorrespondenceInsertLinkBE
    {
        /// <summary>
        /// Gets or sets the URL for a Correspondence link .
        /// </summary>
        public InsertCorrespondenceLinkServiceCodeBE Service { get; set; }

        /// <summary>
        /// Gets or sets the URL for a Correspondence link .
        /// </summary>
        public InsertCorrespondenceLinkArchiveRefBE ArchiveReference { get; set; }

        /// <summary>
        /// Gets or sets the URL for a Correspondence link .
        /// </summary>
        public InsertCorrespondenceLinkServiceURLBE URL { get; set; }

        /// <summary>
        /// Gets or sets the URL for a Correspondence link .
        /// </summary>
        public ReplyOptionType ReplyOptionType { get; set; }
    }

    /// <remarks>
    /// The CorrespondenceLinkListBE inherits List CorrespondenceLinkBE and
    /// contains a number of CorrespondenceLinkBE objects.
    /// In addition to the objects themselves, it contains LimitReached property which indicates if the result set size
    /// exceeded the maximum number of elements.
    /// </remarks>
    /// <summary>Collection of CorrespondenceLinkBE entities</summary>
    public class CorrespondenceInsertLinkBEList : List<CorrespondenceInsertLinkBE>
    {
        /// <summary>
        /// Gets or sets a value indicating whether the query returned more rows than specified as the maximum number of rows.
        /// </summary>
        public bool LimitReached { get; set; }
    }
}