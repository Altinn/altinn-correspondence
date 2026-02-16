using Altinn.Correspondence.Core.Models.Register;
using Altinn.Platform.Register.Models;

namespace Altinn.Correspondence.Core.Services;
public interface IAltinnRegisterService
{
    Task<int?> LookUpPartyId(string identificationId, CancellationToken cancellationToken);
    Task<string?> LookUpName(string identificationId, CancellationToken cancellationToken);
    Task<Party?> LookUpPartyByPartyId(int partyId, CancellationToken cancellationToken);
    Task<Party?> LookUpPartyByPartyUuid(Guid partyUuid, CancellationToken cancellationToken);    
    Task<Party?> LookUpPartyById(string identificationId, CancellationToken cancellationToken);
    Task<PartyV2?> LookUpPartyV2ById(string identificationId, CancellationToken cancellationToken = default);
    Task<List<PartyV2>?> LookUpPartiesByIds(List<string> identificationIds, CancellationToken cancellationToken);
    Task<List<RoleItem>> LookUpPartyRoles(string partyUuid, CancellationToken cancellationToken);
    Task<List<MainUnitItem>> LookUpMainUnits(string urn, CancellationToken cancellationToken);
}
