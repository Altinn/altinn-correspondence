using System.Net.Http.Json;
using System.Text.RegularExpressions;

using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Services;
using Altinn.Platform.Register.Models;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Correspondence.Integrations.Altinn.Register;
public class AltinnRegisterService : IAltinnRegisterService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AltinnRegisterService> _logger;

    public AltinnRegisterService(HttpClient httpClient, IOptions<AltinnOptions> altinnOptions, ILogger<AltinnRegisterService> logger)
    {
        httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", altinnOptions.Value.PlatformSubscriptionKey);
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string?> LookUpPartyId(string identificationId, CancellationToken cancellationToken = default)
    {
        var organizationWithPrefixFormat = new Regex(@"^\d{4}:\d{9}$");
        var organizationWithoutPrefixFormat = new Regex(@"^\d{9}$");
        if (organizationWithPrefixFormat.IsMatch(identificationId))
        {
            identificationId = identificationId.Substring(5);
        }
        var personFormat = new Regex(@"^\d{11}$");
        if (!personFormat.IsMatch(identificationId) && !organizationWithoutPrefixFormat.IsMatch(identificationId))
        {
            _logger.LogError("identificationId is not a valid organization or person number.");
            return null;
        }

        var partyLookup = new PartyLookup()
        {
            OrgNo = organizationWithoutPrefixFormat.IsMatch(identificationId) ? identificationId : null,
            Ssn = personFormat.IsMatch(identificationId) ? identificationId : null
        };
        var response = await _httpClient.PostAsJsonAsync("register/api/v1/parties/lookup", partyLookup, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Error when looking up organization in Altinn Register.Statuscode was: {statusCode}, error was: {error}", response.StatusCode, await response.Content.ReadAsStringAsync());
            return null;
        }
        var party = await response.Content.ReadFromJsonAsync<Party>();
        if (party is null)
        {
            _logger.LogError("Unexpected json response when looking up organization in Altinn Register");
            return null;
        }
        return party.PartyId.ToString();
    }
}
