using System.Text.RegularExpressions;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Services;

namespace Altinn.Correspondence.Integrations.Altinn.Register;
public class AltinnRegisterDevService : IAltinnRegisterService
{
    private const string _identificationIDPattern = @"^(?:\d{11}|\d{9}|\d{4}:\d{9})$";
    private static readonly Regex IdentificationIDRegex = new(_identificationIDPattern);
    private readonly int _digdirPartyId = 50952483;
    public Task<string?> LookUpPartyId(string identificationId, CancellationToken cancellationToken)
    {
        if (IdentificationIDRegex.IsMatch(identificationId))
        {
            return Task.FromResult<string?>(_digdirPartyId.ToString());
        }
        return Task.FromResult<string?>(null);
    }
    public Task<string?> LookUpName(string identificationId, CancellationToken cancellationToken)
    {
        if (IdentificationIDRegex.IsMatch(identificationId))
        {
            return Task.FromResult<string?>("Digitaliseringsdirektoratet");
        }
        return Task.FromResult<string?>(null);
    }

    public Task<Party?> LookUpPartyById(string identificationId, CancellationToken cancellationToken)
    {
        if (IdentificationIDRegex.IsMatch(identificationId.WithoutPrefix()))
        {
            return Task.FromResult<Party?>(new Party
            {
                PartyId = _digdirPartyId,
                OrgNumber = "991825827",
                SSN = "",
                Resources = new List<string>(),
                PartyTypeName = PartyType.Organization,
                UnitType = "Virksomhet",
                Name = "Digitaliseringsdirektoratet",
                PartyUuid = Guid.NewGuid(),
            });
        }
        return Task.FromResult<Party?>(null);
    }

    public Task<Party?> LookUpPartyByPartyId(int partyId, CancellationToken cancellationToken)
    {
        var party = new Party
        {
            PartyId = _digdirPartyId,
            OrgNumber = "991825827",
            SSN = "",
            Resources = new List<string>(),
            PartyTypeName = PartyType.Organization,
            UnitType = "Virksomhet",
            Name = "Digitaliseringsdirektoratet",
            PartyUuid = Guid.NewGuid(),
        };
        return Task.FromResult<Party?>(party);
    }
}
