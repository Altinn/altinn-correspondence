
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;

namespace Altinn.Correspondence.Integrations.Altinn.AccessManagement;

public class AltinnAccessManagementDevService : IAltinnAccessManagementService
{
    public Task<List<Party>> GetAuthorizedParties(Party partyToRequestFor, CancellationToken cancellationToken = default)
    {
        Party party = new()
        {
            PartyId = 50167512,
            OrgNumber = "991825827",
            SSN = "",
            Resources = new List<string>(),
            PartyTypeName = PartyType.Organization,
            UnitType = "Virksomhet",
            Name = "Digitaliseringsdirektoratet",
        };
        return Task.FromResult(new List<Party> { party });
    }
}