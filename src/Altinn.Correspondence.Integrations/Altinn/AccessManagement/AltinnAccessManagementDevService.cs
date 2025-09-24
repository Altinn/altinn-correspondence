
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Repositories;

namespace Altinn.Correspondence.Integrations.Altinn.AccessManagement;

public class AltinnAccessManagementDevService : IAltinnAccessManagementService
{
    private readonly int _digdirPartyId = 50952483;
    public Task<List<PartyWithSubUnits>> GetAuthorizedParties(Party partyToRequestFor, string? userId, CancellationToken cancellationToken = default)
    {
        PartyWithSubUnits party = new()
        {
            PartyId = _digdirPartyId,
            OrgNumber = "991825827",
            SSN = "",
            Resources = new List<string>(),
            PartyTypeName = PartyType.Organization,
            UnitType = "Virksomhet",
            Name = "Digitaliseringsdirektoratet",
        };
        return Task.FromResult(new List<PartyWithSubUnits> { party });
    }
}