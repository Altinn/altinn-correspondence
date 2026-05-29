using Altinn.Register.Contracts;

namespace Altinn.Correspondence.Core.Repositories;

public interface IAltinnAccessManagementService
{
    Task<HashSet<int>> GetAuthorizedPartyIds(Party partyToRequestFor, string? userId, CancellationToken cancellationToken = default);
}
