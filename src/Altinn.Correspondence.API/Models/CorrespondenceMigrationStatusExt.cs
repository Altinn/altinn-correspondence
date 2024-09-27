using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Altinn.Correspondence.Application.InitializeCorrespondence;
using Newtonsoft.Json;

namespace Altinn.Correspondence.API.Models
{
    /// <summary>
    /// This class is used by the Altinn 2 migration task to retrieve status for an Altinn 2 correspondence in Altinn 3
    /// This is useful when the migration has experienced an Error or when the migration has otherwise been interrupted, in order to only transfer previously untransferred attachments to an existing correspondence.
    /// </summary>
    public class CorrespondenceMigrationStatusExt
    {
        /// <summary>
        /// Unique Altinn 2 Id for this correspondence
        /// </summary>
        [JsonPropertyName("altinn2CorrespondenceId")]
        public required int Altinn2CorrespondenceId { get; set; }

        /// <summary>
        /// Unique Altinn 3 Id for this correspondence
        /// </summary>
        [JsonPropertyName("correspondenceId")]
        public required Guid CorrespondenceId { get; set; }

        /// <summary>
        /// Current state of attachment statuses for the correspondence
        /// </summary>
        [JsonPropertyName("attachmentStatuses")]
        public List<AttachmentMigrationStatusExt>? AttachmentStatuses { get; set; } = new List<AttachmentMigrationStatusExt>();
    }
}