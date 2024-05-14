using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Core.Models
{
    public class ExternalReferenceEntity
    {
        [Key]
        public Guid Id { get; set; }

        public required string ReferenceValue { get; set; }

        public required ReferenceType ReferenceType { get; set; }

        public Guid CorrespondenceId { get; set; }
        [ForeignKey("CorrespondenceId")]
        public CorrespondenceEntity Correspondence { get; set; }
    }
}