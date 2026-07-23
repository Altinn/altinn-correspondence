using System.Net.Http.Json;
using Altinn.Correspondence.Core.Models.Profile;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Services;
using Microsoft.Extensions.Options;

namespace Altinn.Correspondence.Integrations.Altinn.Profile;

public class AltinnProfileService : IAltinnProfileService
{
    private readonly HttpClient _httpClient;

    public AltinnProfileService(HttpClient httpClient, IOptions<AltinnOptions> altinnOptions)
    {
        httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", altinnOptions.Value.PlatformSubscriptionKey);
        _httpClient = httpClient;
    }

    public async Task<List<UnitContactPoints>> GetUserRegisteredContactPoints(List<string> organizationNumbers, string resourceId, CancellationToken cancellationToken)
    {
        var request = new UnitContactPointsRequest
        {
            OrganizationNumbers = organizationNumbers,
            ResourceId = resourceId
        };
        var response = await _httpClient.PostAsJsonAsync("profile/api/v1/correspondence/units/contactpoint/lookup", request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Error when looking up user registered contact points in Altinn Profile. Statuscode was: {response.StatusCode}, error was: {await response.Content.ReadAsStringAsync(cancellationToken)}");
        }

        var result = await response.Content.ReadFromJsonAsync<UnitContactPointsResponse>(cancellationToken);
        if (result is null)
        {
            throw new Exception("Unexpected json response when looking up user registered contact points in Altinn Profile");
        }

        return result.ContactPointsList;
    }

    public async Task<List<OrgNotificationAddresses>> GetOrganizationNotificationAddresses(List<string> organizationNumbers, CancellationToken cancellationToken)
    {
        var request = new OrgNotificationAddressesRequest
        {
            OrganizationNumbers = organizationNumbers
        };
        var response = await _httpClient.PostAsJsonAsync("profile/api/v1/correspondence/organizations/notificationaddresses/lookup", request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"Error when looking up organization notification addresses in Altinn Profile. Statuscode was: {response.StatusCode}, error was: {await response.Content.ReadAsStringAsync(cancellationToken)}");
        }

        var result = await response.Content.ReadFromJsonAsync<OrgNotificationAddressesResponse>(cancellationToken);
        if (result is null)
        {
            throw new Exception("Unexpected json response when looking up organization notification addresses in Altinn Profile");
        }

        return result.ContactPointsList;
    }
}
