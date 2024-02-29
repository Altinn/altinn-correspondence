namespace Altinn.Correspondence.API.Models.Enums
{
    /// <summary>
    /// Defines the possible types of replies.
    /// </summary>
    public enum ReplyOptionTypeExternal : int
    {
        /// <summary>
        /// Specifies a reply with a reference to a form.
        /// </summary>
        Form = 1, 

        /// <summary>
        /// Specifies a reply with a reference to an external service code.
        /// </summary>
        ServiceCode = 2, 

        /// <summary>
        /// Specify a reply with a reference to an external to a service url.
        /// </summary>
        ServiceURL = 3, 

        /// <summary>
        /// Specifies a reply wit an archive reference.
        /// </summary>
        ArchiveReference = 4, 
    }
}