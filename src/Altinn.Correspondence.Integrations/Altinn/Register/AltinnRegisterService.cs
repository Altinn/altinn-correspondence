﻿using System.Globalization;
using System.Net.Http.Json;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Services;
using Altinn.Platform.Register.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Party = Altinn.Correspondence.Core.Models.Entities.Party;

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
        var party = await LookUpPartyById(identificationId, cancellationToken);
        return party?.PartyId.ToString();
    }

    public async Task<string?> LookUpName(string identificationId, CancellationToken cancellationToken = default)
    {
        var party = await LookUpPartyById(identificationId, cancellationToken);
        return party?.Name;
    }

    public async Task<Party?> LookUpPartyByPartyId(int partyId, CancellationToken cancellationToken = default)
    {
        if (partyId <= 0)
        {
            _logger.LogError("partyId is not a valid number.");
            return null;
        }

        var response = await _httpClient.GetAsync($"register/api/v1/parties/{partyId}", cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Error when looking up party in Altinn Register.Statuscode was: {statusCode}, error was: {error}", response.StatusCode, await response.Content.ReadAsStringAsync());
            return null;
        }
        var party = await response.Content.ReadFromJsonAsync<Party>();
        if (party is null)
        {
            _logger.LogError("Unexpected json response when looking up Party in Altinn Register");
            return null;
        }

        return party;
    }
    public async Task<Party?> LookUpPartyById(string identificationId, CancellationToken cancellationToken = default)
    {
        identificationId = identificationId.WithoutPrefix();

        var partyLookup = new PartyLookup()
        {
            OrgNo = identificationId.IsOrganizationNumber() ? identificationId : null,
            Ssn = identificationId.IsSocialSecurityNumber() ? identificationId : null
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
        return party;
    }
    public async Task<List<Party>?> LookUpPartiesByIds(List<string> identificationIds, CancellationToken cancellationToken = default)
    {
        var organizations = identificationIds.Where(x => x.IsOrganizationNumber()).Select(x => new PartyLookup() { OrgNo = x }).ToList();
        var socialSecurityNumbers = identificationIds.Where(x => x.IsSocialSecurityNumber()).Select(x => new PartyLookup() { Ssn = x }).ToList();
        var partyLookup = new PartyNamesLookup()
        {
            Parties = organizations.Concat(socialSecurityNumbers).ToList()
        };
        var response = await _httpClient.PostAsJsonAsync("register/api/v1/parties/nameslookup", partyLookup, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Error when looking up party names in Altinn Register.Statuscode was: {statusCode}, error was: {error}", response.StatusCode, await response.Content.ReadAsStringAsync());
            return null;
        }
        var parties = await response.Content.ReadFromJsonAsync<PartyNamesLookupResult>();
        if (parties is null)
        {
            _logger.LogError("Unexpected json response when looking up party names in Altinn Register");
            return null;
        }

        return parties.PartyNames.Select(x => new Party
        {
            OrgNumber = x.OrgNo,
            SSN = x.Ssn,
            Name = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(x.Name.ToLower())
        }).ToList();
    }
}
