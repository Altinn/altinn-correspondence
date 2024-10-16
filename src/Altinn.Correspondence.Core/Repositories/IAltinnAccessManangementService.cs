using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Core.Repositories;

public interface IAltinnAccessManagementService
{
    Task<List<SimpleParty>> GetAutorizedParties(SimpleParty partyToRequestFor, CancellationToken cancellationToken = default);
}
