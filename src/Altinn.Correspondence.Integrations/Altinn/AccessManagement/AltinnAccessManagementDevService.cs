using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Integrations.Altinn.Register;
using Altinn.Register.Contracts;

namespace Altinn.Correspondence.Integrations.Altinn.AccessManagement;

public class AltinnAccessManagementDevService : IAltinnAccessManagementService
{
    public Task<HashSet<int>> GetAuthorizedPartyIds(Party partyToRequestFor, string? userId, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new HashSet<int> { AltinnRegisterDevService.DigdirPartyId });
    }
}
