using Altinn.Correspondence.Core.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Altinn.Correspondence.Core.Models
{
    public class CorrespondenceContentEntity
    {
        [Key]
        public Guid Id { get; set; }

        public string Language { get; set; }

        public string MessageTitle { get; set; }

        public string MessageSummary { get; set; }

        public List<CorrespondenceAttachmentEntity> Attachments { get; set; }

        public Guid CorrespondenceId { get; set; }
        [ForeignKey("CorrespondenceId")]
        public CorrespondenceEntity Correspondence { get; set; }
    }
}