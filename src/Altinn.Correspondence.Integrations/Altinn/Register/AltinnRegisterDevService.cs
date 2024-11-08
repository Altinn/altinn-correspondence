using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Services;

namespace Altinn.Correspondence.Integrations.Altinn.Register;
public class AltinnRegisterDevService : IAltinnRegisterService
{
    public Task<string?> LookUpPartyId(string identificationId, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>("50167512");
    }
    public Task<string?> LookUpName(string identificationId, CancellationToken cancellationToken)
    {
        return Task.FromResult<string?>("Digitaliseringsdirektoratet");
    }

    public Task<Party?> LookUpPartyById(string identificationId, CancellationToken cancellationToken)
    {
        var party = new Party
        {
            PartyId = 50167512,
            OrgNumber = "0192:991825827",
            SSN = "",
            Resources = new List<string>(),
            PartyTypeName = PartyType.Organization,
            UnitType = "Virksomhet",
            Name = "Digitaliseringsdirektoratet",
        };
        return Task.FromResult<Party?>(party);
    }

    public Task<Party?> LookUpPartyByPartyId(int partyId, CancellationToken cancellationToken)
    {
        var party = new Party
        {
            PartyId = 50167512,
            OrgNumber = "0192:991825827",
            SSN = "",
            Resources = new List<string>(),
            PartyTypeName = PartyType.Organization,
            UnitType = "Virksomhet",
            Name = "Digitaliseringsdirektoratet",
        };
        return Task.FromResult<Party?>(party);
    }
}
