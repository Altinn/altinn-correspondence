using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Core.Models.Entities
{
    public class LegacyPartyEntity
    {
        [Key]
        public Guid Id { get; set; }
        public int PartyId { get; set; }
    }
}
