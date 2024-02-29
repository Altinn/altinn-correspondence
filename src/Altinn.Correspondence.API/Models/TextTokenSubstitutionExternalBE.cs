namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents a TextToken. TextTokens is used to create more dynamic notification texts. A Token can trigger text substitution
    /// between a token number and a token value. 
    /// </summary>
    public class TextTokenSubstitutionExternalBE
    {
        /// <summary>
        /// Gets or sets the id of a specific text token.
        /// </summary>
        /// <remarks>
        /// This property is hidden from external systems.
        /// </remarks>
        public int TextTokenSubstitutionID { get; set; }

        /// <summary>
        /// Gets or sets the token number. 
        /// </summary>
        public int TokenNum { get; set; }

        /// <summary>
        /// Gets or sets the token value.
        /// </summary>
        public string TokenValue { get; set; }

        /// <summary>
        /// Gets or sets the id of the notification.
        /// </summary>
        /// <remarks>
        /// This property is hidden from external systems.
        /// </remarks>
        public int NotificationID { get; set; }
    }

    /// <summary>
    /// Represents a strongly typed list of TextToken elements that can be accessed by index.
    /// </summary>
    public class TextTokenSubstitutionExternalBEList : List<TextTokenSubstitutionExternalBE>
    {
        /// <summary>
        /// Gets or sets a value indicating whether the list is incomplete. This is true if there exists more TextToken elements than what is found in the list.
        /// </summary>
        public bool LimitReached { get; set; }
    }
}