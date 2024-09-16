namespace Altinn.Correspondence.Core.Models.Enums
{
    /// <summary>
    /// Enum describing available notification channels.
    /// </summary>
    public enum NotificationTemplate
    {
        /// <summary>
        /// Fully customizable template.
        /// </summary>
        TextTokenOnly,

        /// <summary>
        /// Standard Altinn notification template for a person.
        /// </summary>
        GenericPersonMessage,

        /// <summary>
        /// Standard Altinn notification template for a organization.
        /// </summary>
        GenericOrganizationMessage
    }
}