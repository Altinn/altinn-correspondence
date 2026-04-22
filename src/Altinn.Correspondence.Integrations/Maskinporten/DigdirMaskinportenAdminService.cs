using Altinn.ApiClients.Maskinporten.Config;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace Altinn.Correspondence.Integrations.Maskinporten;

public class DigdirMaskinportenAdminService(
    IHttpClientFactory httpClientFactory,
    IOptions<MaskinportenJwkRotationSettings> rotationSettings,
    IOptions<MaskinportenSettings> maskinportenSettings,
    IMaskinportenTokenService tokenService,
    ILogger<DigdirMaskinportenAdminService> logger) : IDigdirMaskinportenAdminService
{
    public async Task<MaskinportenJwkSet> GetJwksAsync(string clientId, CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(HttpMethod.Get, $"clients/{clientId}/jwks", cancellationToken);
        using var response = await SendAsync(request, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<MaskinportenJwkSet>(cancellationToken: cancellationToken))
            ?? throw new InvalidOperationException($"Unable to deserialize Digdir JWKS for client '{clientId}'.");
    }

    public async Task<MaskinportenJwkSet> UpdateJwksAsync(string clientId, MaskinportenJwkSet jwks, CancellationToken cancellationToken)
    {
        using var request = await CreateRequestAsync(HttpMethod.Post, $"clients/{clientId}/jwks", cancellationToken);
        request.Content = JsonContent.Create(jwks);
        using var response = await SendAsync(request, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<MaskinportenJwkSet>(cancellationToken: cancellationToken))
            ?? throw new InvalidOperationException($"Unable to deserialize updated Digdir JWKS for client '{clientId}'.");
    }

    private async Task<HttpRequestMessage> CreateRequestAsync(HttpMethod method, string relativePath, CancellationToken cancellationToken)
    {
        var settings = rotationSettings.Value;
        var maskinporten = maskinportenSettings.Value;
        var accessToken = await tokenService.RequestTokenAsync(
            settings.AdminClientId,
            settings.AdminEncodedJwk,
            settings.AdminScope,
            maskinporten.Environment,
            cancellationToken);

        var request = new HttpRequestMessage(method, $"{GetDigdirApiBaseUrl(settings.AdminApiBaseUrl, maskinporten.Environment)}/{relativePath}");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    private async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var httpClient = httpClientFactory.CreateClient();
        var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            logger.LogError("Digdir admin request failed. Method={Method}, Url={Url}, Status={StatusCode}, Body={Body}",
                request.Method, request.RequestUri, (int)response.StatusCode, body);
            throw new InvalidOperationException($"Digdir admin request failed with status {(int)response.StatusCode}: {body}");
        }

        return response;
    }

    private static string GetDigdirApiBaseUrl(string configuredBaseUrl, string environment)
    {
        if (!string.IsNullOrWhiteSpace(configuredBaseUrl))
        {
            return configuredBaseUrl.TrimEnd('/');
        }

        return environment.Equals("prod", StringComparison.OrdinalIgnoreCase)
            || environment.Equals("production", StringComparison.OrdinalIgnoreCase)
            ? "https://api.samarbeid.digdir.no/external/api/v1"
            : "https://api.test.samarbeid.digdir.no/external/api/v1";
    }
}
