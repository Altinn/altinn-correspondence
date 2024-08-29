using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Altinn.Correspondence.API.Models.Enums;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// Represents an attachment to a specific correspondence as part of Initialize Correpondence Operation
    /// </summary>
    public class InitializeCorrespondenceAttachmentExt : BaseAttachmentExt
    {
        /// <summary>
        /// A unique id for the correspondence attachment.
        /// 
        [JsonPropertyName("id")]
        public Guid Id { get; set; }

        /// <summary>
        /// Specifies the location type of the attachment data
        /// </summary>
        [JsonPropertyName("dataLocationType")]
        [Required]
        public InitializeAttachmentDataLocationTypeExt DataLocationType { get; set; }

        /// <summary>
        /// Specifies the location url of the attachment data
        /// </summary>
        /// <remarks>
        /// Required only if AttachmentDataLocationType is ExistingCorrespondenceAttachment or ExisitingExternalStorage
        /// If the type is NewCorrespondenceAttachmentBlob, this requires the attachment data to be uploaded using the AttachmentId returned from the Initialize operation
        /// </remarks>
        [JsonPropertyName("dataLocationUrl")]
        public string? DataLocationUrl { get; set; }

        /// <summary>
        /// The expiration time of the attachment
        /// </summary>
        [JsonPropertyName("expirationTime")]
        public DateTimeOffset ExpirationTime { get; set; }
    }
}