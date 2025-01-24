using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Integrations.Altinn.Register;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace Altinn.Correspondence.Integrations.Altinn.Storage;

public class AltinnStorageService : IAltinnStorageService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AltinnRegisterService> _logger;

    public AltinnStorageService(HttpClient httpClient, IOptions<AltinnOptions> altinnOptions, ILogger<AltinnRegisterService> logger)
    {
        httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", altinnOptions.Value.PlatformSubscriptionKey);
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<bool> AddPartyToSblBridge(int partyId, CancellationToken cancellationToken)
    {
        if (partyId <= 0)
        {
            return false;
        }
        using var response = await _httpClient.PostAsJsonAsync($"storage/api/v1/sblbridge/correspondencerecipient?partyId={partyId}", new SblBridgeParty()
        {
            PartyId = partyId
        }, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var statusCode = response.StatusCode;
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new Exception($"Error when adding party to SBL Bridge through Storage. Statuscode was: ${statusCode}, error was: ${errorContent}");
        }
        return true;
    }
}
