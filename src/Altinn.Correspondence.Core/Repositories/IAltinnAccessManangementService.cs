using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Core.Repositories;

public interface IAltinnAccessManagementService
{
    Task<List<Party>> GetAutorizedParties(Party partyToRequestFor, CancellationToken cancellationToken = default);
}
