namespace Altinn.Correspondence.Core.Models.Entities
{
    /// <summary>
    /// Class representing a party
    /// </summary>
    public class PartyWithSubUnits : Party
    {
        public List<PartyWithSubUnits> SubUnits { get; set; } = new List<PartyWithSubUnits>();
    }
}
