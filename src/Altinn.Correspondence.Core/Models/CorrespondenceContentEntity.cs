using Altinn.Correspondence.Core.Models.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Altinn.Correspondence.Core.Models
{
    public class CorrespondenceContentEntity
    {
        [Key]
        public Guid Id { get; set; }

        public required string Language { get; set; }

        public required string MessageTitle { get; set; }

        public required string MessageSummary { get; set; }

        public required string MessageBody { get; set; }

        public required List<CorrespondenceAttachmentEntity> Attachments { get; set; }

        public Guid CorrespondenceId { get; set; }
        [ForeignKey("CorrespondenceId")]
        public CorrespondenceEntity? Correspondence { get; set; }
    }
}