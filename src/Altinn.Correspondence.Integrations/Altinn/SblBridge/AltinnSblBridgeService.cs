using System.Net.Http.Headers;
using System.Text;
using Altinn.Correspondence.Core.Services;


namespace Altinn.Correspondence.Integrations.Altinn.Register;
public class AltinnSblBridgeService : IAltinnSblBridgeService
{
    private readonly HttpClient _httpClient;

    public AltinnSblBridgeService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<bool> AddPartyToSblBridge(int partyId, CancellationToken cancellationToken = default)
    {
        if (partyId <= 0)
        {
            return false;
        }
        Console.WriteLine("Adding party to SBL Bridge");
        Console.WriteLine($"PartyId: {partyId}");
        Console.WriteLine($"BaseAddress: {_httpClient.BaseAddress}");
        StringContent content = new StringContent(partyId.ToString(), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"authorization/api/partieswithmessages", content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var statusCode = response.StatusCode;
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new Exception($"Error when adding party to SBL Bridge. Statuscode was: ${statusCode}, error was: ${errorContent}");
        }
        Console.WriteLine(await response.Content.ReadAsStringAsync(cancellationToken));

        return true;
    }


}
