using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Register;

namespace Altinn.Correspondence.Core.Services;
public interface IAltinnRegisterService
{
    Task<int?> LookUpPartyId(string identificationId, CancellationToken cancellationToken);
    Task<string?> LookUpName(string identificationId, CancellationToken cancellationToken);
    Task<Party?> LookUpPartyByPartyId(int partyId, CancellationToken cancellationToken);
    Task<Party?> LookUpPartyByPartyUuid(Guid partyUuid, CancellationToken cancellationToken);    
    Task<Party?> LookUpPartyById(string identificationId, CancellationToken cancellationToken);
    Task<List<Party>?> LookUpPartiesByIds(List<string> identificationIds, CancellationToken cancellationToken);
    Task<List<RoleItem>> LookUpPartyRoles(string partyUuid, CancellationToken cancellationToken);
    Task<List<MainUnitItem>> LookUpMainUnits(string urn, CancellationToken cancellationToken);
}
