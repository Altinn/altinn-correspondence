using Altinn.Correspondence.Core.Models.Register;
using Altinn.Register.Contracts;

namespace Altinn.Correspondence.Core.Services;

public interface IAltinnRegisterService
{
    /// <summary>
    /// Looks up a single party using any supported identifier (org number, SSN,
    /// party id, party uuid, or URN). The identifier is normalized to a URN before
    /// being sent to the v2 query endpoint.
    /// </summary>
    Task<Party?> LookUpPartyById(string identificationId, CancellationToken cancellationToken);

    /// <summary>
    /// Looks up multiple parties in one round-trip via the v2 query endpoint.
    /// </summary>
    Task<List<Party>?> LookUpPartiesByIds(List<string> identificationIds, CancellationToken cancellationToken);

    Task<List<RoleItem>> LookUpPartyRoles(string partyUuid, CancellationToken cancellationToken);

    Task<List<MainUnitItem>> LookUpMainUnits(string urn, CancellationToken cancellationToken);
}
