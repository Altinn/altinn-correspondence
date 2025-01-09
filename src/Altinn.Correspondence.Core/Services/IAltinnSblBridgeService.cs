using Altinn.Correspondence.Core.Models.Entities;

namespace Altinn.Correspondence.Core.Services;
public interface IAltinnSblBridgeService
{
    Task<bool> AddPartyToSblBridge(int partyId, CancellationToken cancellationToken);

}
