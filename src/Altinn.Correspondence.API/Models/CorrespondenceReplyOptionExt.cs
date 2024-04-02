namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents a ReplyOption with information provided by the sender.
    /// A reply option is a way for recipients to respond to a correspondence in additon to the normal Read and Confirm operations
    /// </summary>
    public class CorrespondenceReplyOptionExt
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