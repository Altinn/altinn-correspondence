using Altinn.Correspondence.Core.Models.AccessManagement;
using Altinn.Platform.Register.Models;

namespace Altinn.Correspondence.Core.Repositories;

public interface IAltinnAccessManagementService
{
    Task<List<AuthorizedParty>> GetAuthorizedParties(Party partyToRequestFor, string? userId, CancellationToken cancellationToken = default);
}
