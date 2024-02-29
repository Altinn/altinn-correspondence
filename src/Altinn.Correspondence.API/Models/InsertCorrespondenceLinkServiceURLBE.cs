namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents a reply option of the type URL.
    /// </summary>
    public class InsertCorrespondenceLinkServiceURLExternalBE
    {
        /// <summary>
        /// Gets or sets the URL to be used as a reply/response to a correspondence. 
        /// </summary>
        public string LinkURL { get; set; }

        /// <summary>
        /// Gets or sets the url text.
        /// </summary>
        public string LinkText { get; set; }
    }
}