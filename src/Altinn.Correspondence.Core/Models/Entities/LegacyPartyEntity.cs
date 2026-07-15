using System.ComponentModel.DataAnnotations;

namespace Altinn.Correspondence.Core.Models.Entities
{
    public class LegacyPartyEntity
    {
        [Key]
        public Guid Id { get; set; }
        public int PartyId { get; set; }
    }
}
