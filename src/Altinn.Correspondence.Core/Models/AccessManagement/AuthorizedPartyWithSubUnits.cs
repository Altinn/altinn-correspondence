namespace Altinn.Correspondence.Core.Models.AccessManagement
{
    /// <summary>
    /// Class representing a party
    /// </summary>
    public class AuthorizedPartyWithSubUnits : AuthorizedParty
    {
        public List<AuthorizedPartyWithSubUnits> SubUnits { get; set; } = new List<AuthorizedPartyWithSubUnits>();
    }
}
