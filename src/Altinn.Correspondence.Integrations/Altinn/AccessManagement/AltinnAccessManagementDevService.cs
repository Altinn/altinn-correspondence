
using Altinn.Correspondence.Core.Models.AccessManagement;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Platform.Register.Models;

namespace Altinn.Correspondence.Integrations.Altinn.AccessManagement;

public class AltinnAccessManagementDevService : IAltinnAccessManagementService
{
    private readonly int _digdirPartyId = 50952483;
    public Task<List<AuthorizedParty>> GetAuthorizedParties(Party partyToRequestFor, string? userId, CancellationToken cancellationToken = default)
    {
        AuthorizedPartyWithSubUnits party = new()
        {
            PartyId = _digdirPartyId,
            OrgNumber = "991825827",
            SSN = "",
            Resources = new List<string>(),
            PartyTypeName = AuthorizedPartyType.Organization,
            UnitType = "Virksomhet",
            Name = "Digitaliseringsdirektoratet",
        };
        return Task.FromResult(new List<AuthorizedParty> { party });
    }
}