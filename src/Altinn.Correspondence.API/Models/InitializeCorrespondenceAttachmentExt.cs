using Altinn.Correspondence.API.Models.Enums;
using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents an attachment to a specific correspondence as part of Initialize Correspondence Operation
    /// </summary>
    public class InitializeCorrespondenceAttachmentExt : BaseAttachmentExt
    {
        /// <summary>
        /// A unique id for the correspondence attachment.
        /// </summary>
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Specifies the location type of the attachment data
        /// </summary>
        [JsonPropertyName("dataLocationType")]
        [DefaultValue(InitializeAttachmentDataLocationTypeExt.NewCorrespondenceAttachment)]
        public InitializeAttachmentDataLocationTypeExt DataLocationType { get; set; } = InitializeAttachmentDataLocationTypeExt.NewCorrespondenceAttachment;
    }
}