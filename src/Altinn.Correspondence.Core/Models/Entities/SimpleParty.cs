using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Core.Models.Entities
{
    /// <summary>
    /// An simple version of the Party Entity representing a single party
    /// </summary>
    /// <remarks>
    /// TODO: Consider removing this and instead using the Register Party entity directly.
    /// </remarks>
    public class SimpleParty
    {
        /// <summary>
        /// Gets or sets the ID of the party
        /// </summary>
        public int PartyId { get; set; }

        /// <summary>
        /// Gets or sets the UUID of the party
        /// </summary>
        public Guid? PartyUuid { get; set; }

        /// <summary>
        /// Gets or sets the type of party
        /// </summary>
        public SimplePartyType PartyTypeName { get; set; }

        /// <summary>
        /// Gets the parties org number
        /// </summary>
        public string OrgNumber { get; set; }

        /// <summary>
        /// Gets the parties ssn
        /// </summary>
        public string SSN { get; set; }

        public List<string>? Resources { get; set; }

        public SimpleParty(int partyId, Guid? partyUuid, SimplePartyType partyTypeName, string orgNumber, string sSN, List<string>? resources = null)
        {
            PartyId = partyId;
            PartyUuid = partyUuid;
            PartyTypeName = partyTypeName;
            OrgNumber = orgNumber;
            SSN = sSN;
            Resources = resources;
        }
    }
}
