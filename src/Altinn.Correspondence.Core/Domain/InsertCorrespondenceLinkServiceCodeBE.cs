namespace Altinn.Correspondence.Core.Domain.Models
{
    /// <summary>
    /// The CorrespondenceLinkBE provides Correspondence link information provided by a Service Owner
    /// Each of the properties listed below has a one-on-one mapping with a column either from ReplyOption or
    /// ReplyOptionType or ServiceEdition or Service table in the Service Engine DB.
    /// </summary>
    public class InsertCorrespondenceLinkServiceCodeBE
    {
        /// <summary>
        /// Gets or sets the URL for a Correspondence link .
        /// </summary>
        public string ServiceCode { get; set; }

        /// <summary>
        /// Gets or sets service edition
        /// </summary>
        public string ServiceEdition { get; set; }
    }
}