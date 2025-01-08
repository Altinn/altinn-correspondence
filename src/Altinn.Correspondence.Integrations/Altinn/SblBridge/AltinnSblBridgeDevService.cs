using Altinn.Correspondence.Core.Services;

namespace Altinn.Correspondence.Integrations.Altinn.SblBridge;
public class AltinnSblBridgeDevService : IAltinnSblBridgeService
{
    private readonly string _baseUrl;
    public AltinnSblBridgeDevService(string baseUrl)
    {
        _baseUrl = baseUrl;
    }

    public async Task<bool> AddPartyToSblBridge(int partyId, CancellationToken cancellationToken = default)
    {
        if (partyId <= 0)
        {
            return false;
        }
        return true;
    }
}
