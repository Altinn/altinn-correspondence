using System.Text.RegularExpressions;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Services;

namespace Altinn.Correspondence.Integrations.Altinn.Register;
public class AltinnRegisterDevService : IAltinnRegisterService
{
    private const string _identificationIDPattern = @"^(?:\d{11}|\d{9}|0192:\d{9})$";
    private static readonly Regex IdentificationIDRegex = new(_identificationIDPattern);
    private readonly int _digdirPartyId = 50952483;
    private readonly Guid _digdirPartyUuid = new Guid("36E2BCC6-D5B8-4399-AA90-4AFEB2D1A0BF");
    private readonly int _delegatedUserPartyid = 100;
    private readonly Guid _delegatedUserPartyUuid = new Guid("358C48B4-74A7-461F-A86F-48801DEEC920");

    public Task<int?> LookUpPartyId(string identificationId, CancellationToken cancellationToken)
    {
        if (IdentificationIDRegex.IsMatch(identificationId))
        {
            return Task.FromResult<int?>(_digdirPartyId);
        }
        return Task.FromResult<int?>(null);
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
                PartyUuid = _digdirPartyUuid,
            });
        }
        return Task.FromResult<Party?>(null);
    }

    public Task<Party?> LookUpPartyByPartyId(int partyId, CancellationToken cancellationToken)
    {
        var party = (Party?)null;
        if (partyId == _digdirPartyId)
        {
            party = new Party
            {
                PartyId = _digdirPartyId,
                OrgNumber = "991825827",
                SSN = "",
                Resources = new List<string>(),
                PartyTypeName = PartyType.Organization,
                UnitType = "Virksomhet",
                Name = "Digitaliseringsdirektoratet",
                PartyUuid = _digdirPartyUuid,
            };
        }
        else if (partyId == _delegatedUserPartyid)
        {
            party = new Party
            {
                PartyId = _delegatedUserPartyid,
                OrgNumber = "",
                SSN = "01018045678",
                Resources = new List<string>(),
                PartyTypeName = PartyType.Person,
                UnitType = "Person",
                Name = "Delegert test bruker",
                PartyUuid = _delegatedUserPartyUuid,
            };
        }
        return Task.FromResult<Party?>(party);
    }

    public Task<Party?> LookUpPartyByPartyUuid(Guid partyUuid, CancellationToken cancellationToken)
    {
        var party = (Party?)null;
        if (partyUuid == _digdirPartyUuid)
        {
            party = new Party
            {
                PartyId = _digdirPartyId,
                OrgNumber = "991825827",
                SSN = "",
                Resources = new List<string>(),
                PartyTypeName = PartyType.Organization,
                UnitType = "Virksomhet",
                Name = "Digitaliseringsdirektoratet",
                PartyUuid = _digdirPartyUuid,
            };
        }
        else if (partyUuid == _delegatedUserPartyUuid)
        {
            party = new Party
            {
                PartyId = _delegatedUserPartyid,
                OrgNumber = "",
                SSN = "01018045678",
                Resources = new List<string>(),
                PartyTypeName = PartyType.Person,
                UnitType = "Person",
                Name = "Delegert test bruker",
                PartyUuid = _delegatedUserPartyUuid,
            };
        }

        return Task.FromResult<Party?>(party);
    }

    public Task<List<Party>?> LookUpPartiesByIds(List<string> identificationIds, CancellationToken cancellationToken)
    {
        var parties = new List<Party>();
        foreach (var id in identificationIds)
        {
            if (IdentificationIDRegex.IsMatch(id.WithoutPrefix()))
            {
                parties.Add(new Party
                {
                    PartyId = _digdirPartyId,
                    OrgNumber = id,
                    SSN = id,
                    Resources = new List<string>(),
                    PartyTypeName = PartyType.Organization,
                    UnitType = "Virksomhet",
                    Name = "Digitaliseringsdirektoratet",
                    PartyUuid = Guid.NewGuid(),
                });
            }
        }
        return Task.FromResult<List<Party>?>(parties);
    }
}
