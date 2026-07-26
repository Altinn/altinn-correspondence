using Altinn.Authorization.ModelUtils;

namespace Altinn.Correspondence.API.Models.Enums
{
    /// <summary>
    /// Enum describing available notification templates.
    /// </summary>
    [StringEnumConverter]
    public enum NotificationTemplateExt
    {
        /// <summary>
        /// Fully customizable template.
        /// </summary>
        CustomMessage,

        /// <summary>
        /// Standard Altinn notification template.
        /// </summary>
        GenericAltinnMessage,

    }
}