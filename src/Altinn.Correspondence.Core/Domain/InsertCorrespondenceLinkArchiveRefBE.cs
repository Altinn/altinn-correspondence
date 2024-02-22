namespace Altinn.Correspondence.Core.Models
{
    /// <summary>
    /// The CorrespondenceLinkBE provides Correspondence link information provided by a Service Owner
    /// Each of the properties listed below has a one-on-one mapping with a column either from ReplyOption or
    /// ReplyOptionType or ServiceEdition or Service table in the Service Engine DB.
    /// </summary>
    public class InsertCorrespondenceLinkArchiveRefBE
    {
        /// <summary>
        /// Gets or sets archive reference
        /// </summary>
        public string ArchiveRef { get; set; }
    }
}