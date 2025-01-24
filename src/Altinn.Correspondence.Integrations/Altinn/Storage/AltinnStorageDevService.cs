using Altinn.Correspondence.Core.Services;

namespace Altinn.Correspondence.Integrations.Altinn.Storage
{
    public class AltinnStorageDevService : IAltinnStorageService
    {
        public async Task<bool> AddPartyToSblBridge(int partyId, CancellationToken cancellationToken = default)
        {
            if (partyId <= 0)
            {
                return false;
            }
            return true;
        }
    }
}
