using Altinn.Correspondence.Core.Models.Enums;

namespace Altinn.Correspondence.Core.Models.Entities
{
    /// <summary>
    /// Class representing a party
    /// </summary>
    public class Party
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
        public PartyType PartyTypeName { get; set; }

        /// <summary>
        /// Gets the parties org number
        /// </summary>
        public string OrgNumber { get; set; }

        /// <summary>
        /// Gets the parties ssn
        /// </summary>
        public string SSN { get; set; }

        /// <summary>
        /// Gets or sets the UnitType
        /// </summary>
        public string UnitType { get; set; }

        /// <summary>
        /// Gets or sets the Name
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the IsDeleted
        /// </summary>
        public bool IsDeleted { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether if the reportee in the list is only there for showing the hierarchy (a parent unit with no access)
        /// </summary>
        public bool OnlyHierarchyElementWithNoAccess { get; set; }

        /// <summary>
        /// Gets or sets the value of ChildParties
        /// </summary>
        public List<Party> ChildParties { get; set; }

        public List<string>? Resources { get; set; }
    }
}
