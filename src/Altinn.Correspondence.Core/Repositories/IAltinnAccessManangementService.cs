using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Core.Repositories;

public interface IAltinnAccessManagementService
{
    Task<List<Party>> GetAuthorizedParties(Party partyToRequestFor, CancellationToken cancellationToken = default);
}
