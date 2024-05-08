using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Altinn.Correspondence.Core.Models
{
    public class CorrespondenceReplyOptionEntity
    {
        [Key]
        public Guid Id { get; set; }

        public string LinkURL { get; set; }

        public string LinkText { get; set; }

        public Guid CorrespondenceId { get; set; }
        [ForeignKey("CorrespondenceId")]
        public CorrespondenceEntity Correspondence { get; set; }
    }
}