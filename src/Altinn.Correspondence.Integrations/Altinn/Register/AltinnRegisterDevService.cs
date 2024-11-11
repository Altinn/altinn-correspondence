using System.Text.RegularExpressions;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Services;

namespace Altinn.Correspondence.Integrations.Altinn.Register;
public class AltinnRegisterDevService : IAltinnRegisterService
{
    public Task<string?> LookUpPartyId(string identificationId, CancellationToken cancellationToken)
    {
        var combinedPattern = @"^(?:\d{11}|\d{9}|\d{4}:\d{9})$";
        var regex = new Regex(combinedPattern);

        if (regex.IsMatch(identificationId))
        {
            return Task.FromResult<string?>("50167512");
        }
        return Task.FromResult<string?>(null);
    }
    public Task<string?> LookUpName(string identificationId, CancellationToken cancellationToken)
    {
        var combinedPattern = @"^(?:\d{11}|\d{9}|\d{4}:\d{9})$";
        var regex = new Regex(combinedPattern);

        if (regex.IsMatch(identificationId))
        {
            return Task.FromResult<string?>("Digitaliseringsdirektoratet");
        }
        return Task.FromResult<string?>(null);
    }

    public Task<Party?> LookUpPartyById(string identificationId, CancellationToken cancellationToken)
    {
        var combinedPattern = @"^(?:\d{11}|\d{9}|\d{4}:\d{9})$";
        var regex = new Regex(combinedPattern);

        if (regex.IsMatch(identificationId))
        {
            return Task.FromResult<Party?>(new Party
            {
                PartyId = 50167512,
                OrgNumber = "0192:991825827",
                SSN = "",
                Resources = new List<string>(),
                PartyTypeName = PartyType.Organization,
                UnitType = "Virksomhet",
                Name = "Digitaliseringsdirektoratet",
            });
        }
        return Task.FromResult<Party?>(null);
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
