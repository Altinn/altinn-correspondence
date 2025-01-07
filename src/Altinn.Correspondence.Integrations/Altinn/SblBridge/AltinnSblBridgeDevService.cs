using System.Net.Http.Json;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Services;
using Altinn.Platform.Register.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Party = Altinn.Correspondence.Core.Models.Entities.Party;

namespace Altinn.Correspondence.Integrations.Altinn.Register;
public class AltinnSblBridgeDevService : IAltinnSblBridgeService
{
    public AltinnSblBridgeDevService(string baseUrl)
    {
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
