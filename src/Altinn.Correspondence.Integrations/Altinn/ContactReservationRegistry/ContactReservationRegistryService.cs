﻿using Altinn.Correspondence.Core.Services;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace Altinn.Correspondence.Integrations.Altinn.ContactReservationRegistry;

/**
 * Also known as "Kontakt- og reservasjonsregisteret" ("KRR").
 * */
public class ContactReservationRegistryService(HttpClient httpClient, ILogger<ContactReservationRegistryService> logger) : IContactReservationRegistryService
{
    public async Task<bool> IsPersonReserved(string ssn)
    {
        var request = new ContactReservationPersonRequest { Personidentifikatorer = [ ssn ] };
        var response = await httpClient.PostAsJsonAsync("rest/v2/personer", request);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Error while calling the KRR API. Status code was: {statusCode}, error was: {error}", response.StatusCode, await response.Content.ReadAsStringAsync());
            throw new HttpRequestException("Error while calling the KRR API.");
        }
        var result = await response.Content.ReadFromJsonAsync<ContactReservationPersonResponse>();
        if (result is null)
        {
            logger.LogError("Unexpected json response when looking up person in KRR");
            throw new HttpRequestException("Unexpected json response when looking up person in KRR");
        }
        return result.Personer[0].Reservasjon == "JA";
    }

    public async Task<List<string>> GetReservedRecipients(List<string> recipients)
    {
        if (recipients.Count > 1000)
        {
            throw new ArgumentException("Maximum number of recipients is 1000"); // Max recipient count for InitializeCorrespondence is 500, should implement batching if that is increased
        }
        var request = new ContactReservationPersonRequest { Personidentifikatorer = recipients };
        var response = await httpClient.PostAsJsonAsync("rest/v2/personer", request);
        if (!response.IsSuccessStatusCode)
        {
            logger.LogError("Error while calling the KRR API. Status code was: {statusCode}, error was: {error}", response.StatusCode, await response.Content.ReadAsStringAsync());
            throw new HttpRequestException("Error while calling the KRR API.");
        }
        var result = await response.Content.ReadFromJsonAsync<ContactReservationPersonResponse>();
        if (result is null)
        {
            logger.LogError("Unexpected json response when looking up person in KRR");
            throw new HttpRequestException("Unexpected json response when looking up person in KRR");
        }
        return result.Personer.Where(p => p.Reservasjon == "JA").Select(p => p.Personidentifikator).ToList();
    }
}
