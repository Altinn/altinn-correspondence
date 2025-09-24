using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Core.Repositories;

public interface IAltinnAccessManagementService
{
    Task<List<PartyWithSubUnits>> GetAuthorizedParties(Party partyToRequestFor, string? userId, CancellationToken cancellationToken = default);
}
