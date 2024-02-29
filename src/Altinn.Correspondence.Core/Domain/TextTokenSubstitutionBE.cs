#region Namespace imports

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

#endregion

namespace Altinn.Correspondence.Core.Domain.Models
{
    /// <summary>
    /// Represents a text replacement rule in notifications. Text in a template is replaced with data in a token.
    /// </summary>
    [Serializable]
    [DataContract(Name = "TextToken", Namespace = "http://schemas.altinn.no/services/ServiceEngine/Notification/2009/10")]
    public class TextTokenSubstitutionBE
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TextTokenSubstitutionBE" /> class.
        /// </summary>
        public TextTokenSubstitutionBE()
        {
        }

        /// <summary>
        /// Gets or sets an id for the text token.
        /// </summary>
        public int TextTokenSubstitutionID { get; set; }

        /// <summary>
        /// Gets or sets the text token number. This is the element that is replaced.
        /// </summary>
        [DataMember]
        public int TokenNum { get; set; }

        /// <summary>
        /// Gets or sets the text token value. This is the value that is inserted in place of the token number.
        /// </summary>
        [DataMember]
        public string TokenValue { get; set; }

        /// <summary>
        /// Gets or sets the id of the notification.
        /// </summary>
        public int NotificationID { get; set; }
    }

    /// <summary>
    /// Represents a strongly typed collection of token elements.</summary>
    [Serializable]
    [CollectionDataContract(Namespace = "http://schemas.altinn.no/services/ServiceEngine/Notification/2009/10")]
    public class TextTokenSubstitutionBEList : List<TextTokenSubstitutionBE>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TextTokenSubstitutionBEList" /> class.
        /// </summary>
        public TextTokenSubstitutionBEList()
        {
        }

        /// <summary>
        /// Gets or sets a value indicating whether the query returned more rows than specified as the maximum number of rows.
        /// </summary>
        [DataMember]
        public bool LimitReached { get; set; }
    }
}