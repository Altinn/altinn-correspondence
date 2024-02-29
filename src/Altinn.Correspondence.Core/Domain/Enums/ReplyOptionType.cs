namespace Altinn.Correspondence.Core.Domain.Models.Enums
{
    /// <summary>
    ///  This defines the possible types of replies, typically Form, Mail, Message and others
    /// </summary>
    public enum ReplyOptionType : int
    {
        /// <summary>
        /// If the reply is through Form.
        /// </summary>
        Form = 1, 

        /// <summary>
        /// If the reply is assigned an external Service code.
        /// </summary>
        ServiceCode = 2, 

        /// <summary>
        /// If the reply is assigned a service URL.
        /// </summary>
        ServiceURL = 3, 

        /// <summary>
        /// If the reply has an archive reference.
        /// </summary>
        ArchiveReference = 4, 
    }
}