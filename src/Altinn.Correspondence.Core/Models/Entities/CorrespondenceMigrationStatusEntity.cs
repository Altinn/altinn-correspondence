using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Core.Models.Entities
{
    /// <summary>
    /// Class used to describe current migration status for an Altinn 2 correspondence.
    /// </summary>
    public class CorrespondenceMigrationStatusEntity
    {
        [Key]
        public Guid? CorrespondenceId { get; set; }

        public int? Altinn2CorrespondenceId { get; set; }

        public CorrespondenceStatus? Status { get; set; }

        public List<AttachmentStatusEntity> AttachmentStatus { get; set; } = new List<AttachmentStatusEntity>();
    }
}
