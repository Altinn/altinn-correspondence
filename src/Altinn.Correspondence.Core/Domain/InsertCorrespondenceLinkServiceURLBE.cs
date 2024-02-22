namespace Altinn.Correspondence.Core.Models
{
    /// <summary>
    /// The CorrespondenceLinkBE provides Correspondence link information provided by a Service Owner
    /// Each of the properties listed below has a one-on-one mapping with a column either from ReplyOption or
    /// ReplyOptionType or ServiceEdition or Service table in the Service Engine DB.
    /// </summary>
    public class InsertCorrespondenceLinkServiceURLBE
    {
        /// <summary>
        /// Gets or sets URL for the correspondence link
        /// </summary>
        public string LinkURL { get; set; }

        /// <summary>
        /// Gets or sets the text for a Correspondence link .
        /// </summary>
        public string LinkText { get; set; }
    }
}