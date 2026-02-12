namespace Altinn.Correspondence.Core.Models.AccessManagement
{
    /// <summary>
    /// Enum containing values for the different types of parties
    /// </summary>
    public enum AuthorizedPartyType
    {
        /// <summary>
        /// Party Type is Person
        /// </summary>
        Person = 1,

        /// <summary>
        /// Party Type is Organization
        /// </summary>
        Organization = 2,

        /// <summary>
        /// Party Type is Self Identified user
        /// </summary>
        SelfIdentified = 3,

        /// <summary>
        /// Party Type is sub unit
        /// </summary>
        SubUnit = 4,

        /// <summary>
        /// Party Type is bankruptcy estate
        /// </summary>
        BankruptcyEstate = 5
    }
}
