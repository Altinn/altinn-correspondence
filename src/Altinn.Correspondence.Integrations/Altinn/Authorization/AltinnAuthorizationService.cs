using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Common.PEP.Helpers;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Common.Helpers.Models;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Integrations.Dialogporten.Mappers;
using Altinn.Correspondence.Integrations.Idporten;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;

namespace Altinn.Correspondence.Integrations.Altinn.Authorization;

public class AltinnAuthorizationService : IAltinnAuthorizationService
{
    private readonly HttpClient _httpClient;
    private readonly IResourceRegistryService _resourceRepository;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly AltinnOptions _altinnOptions;
    private readonly DialogportenSettings _dialogportenSettings;
    private readonly IdportenSettings _idPortenSettings;
    private readonly ILogger<AltinnAuthorizationService> _logger;

    public AltinnAuthorizationService(HttpClient httpClient, IOptions<AltinnOptions> altinnOptions, IOptions<DialogportenSettings> dialogportenSettings, IOptions<IdportenSettings> idPortenSettings, IResourceRegistryService resourceRepository, IHostEnvironment hostEnvironment, ILogger<AltinnAuthorizationService> logger)
    {
        httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", altinnOptions.Value.PlatformSubscriptionKey);
        _altinnOptions = altinnOptions.Value;
        _dialogportenSettings = dialogportenSettings.Value;
        _idPortenSettings = idPortenSettings.Value;
        _httpClient = httpClient;
        _resourceRepository = resourceRepository;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public Task<bool> CheckAccessAsSender(ClaimsPrincipal? user, string resourceId, string sender, string? instance, CancellationToken cancellationToken = default)
        => CheckUserAccess(
            user,
            resourceId,
            sender.WithoutPrefix(),
            instance,
            new List<ResourceAccessLevel> { ResourceAccessLevel.Write },
            cancellationToken);

    public Task<bool> CheckAccessAsSender(ClaimsPrincipal? user, CorrespondenceEntity correspondence, CancellationToken cancellationToken = default) =>
        CheckUserAccess(
            user,
            correspondence.ResourceId,
            correspondence.Sender.WithoutPrefix(),
            correspondence.Id.ToString(),
            new List<ResourceAccessLevel> { ResourceAccessLevel.Write },
            cancellationToken);

    public Task<bool> CheckAccessAsRecipient(ClaimsPrincipal? user, CorrespondenceEntity correspondence, CancellationToken cancellationToken = default) =>
        CheckUserAccess(
            user,
            correspondence.ResourceId,
            correspondence.Recipient.WithoutPrefix(),
            correspondence.Id.ToString(),
            new List<ResourceAccessLevel> { ResourceAccessLevel.Read },
            cancellationToken);

    public Task<bool> CheckAccessAsAny(ClaimsPrincipal? user, string resource, string party, CancellationToken cancellationToken) =>
        CheckUserAccess(
            user,
            resource,
            party,
            null,
            new List<ResourceAccessLevel> { ResourceAccessLevel.Read, ResourceAccessLevel.Write },
            cancellationToken);


    public async Task<int?> CheckUserAccessAndGetMinimumAuthLevel(ClaimsPrincipal? user, string ssn, string resourceId, List<ResourceAccessLevel> rights, string onBehalfOf, CancellationToken cancellationToken = default)
    {
        if (user is null)
        {
            throw new InvalidOperationException("This operation cannot be called outside an authenticated HttpContext");
        }
        var bypassDecision = await EvaluateBypassConditions(user, resourceId, cancellationToken);
        if (bypassDecision is not null)
        {
            return bypassDecision.Value ? 3 : null;
        }
        var actionIds = rights.Select(GetActionId).ToList();
        XacmlJsonRequestRoot jsonRequest = CreateDecisionRequestForLegacy(user, ssn, actionIds, resourceId, onBehalfOf);
        var responseContent = await AuthorizeRequest(jsonRequest, cancellationToken);
        var validationResult = ValidateAuthorizationResponse(responseContent, user);
        if (!validationResult)
        {
            return null;
        }
        int? minLevel = IdportenXacmlMapper.GetMinimumAuthLevel(responseContent, user);
        return minLevel;
    }

    private static XacmlJsonAttributeAssignment GetObligation(string category, List<XacmlJsonObligationOrAdvice> obligations)
    {
        foreach (XacmlJsonObligationOrAdvice obligation in obligations)
        {
            XacmlJsonAttributeAssignment assignment = obligation.AttributeAssignment.FirstOrDefault(a => a.Category.Equals(category));
            if (assignment != null)
            {
                return assignment;
            }
        }

        return null;
    }
    public async Task<Dictionary<(string, string), int?>> CheckUserAccessAndGetMinimumAuthLevelWithMultirequest(ClaimsPrincipal? user, string ssn, List<CorrespondenceEntity> correspondences, CancellationToken cancellationToken = default)
    {
        if (user is null)
        {
            throw new InvalidOperationException("This operation cannot be called outside an authenticated HttpContext");
        }
        if (correspondences.Count == 0)
        {
            return new Dictionary<(string, string), int?>();
        }

        List<(string Recipient, string ResourceId)> recipientWithResources = correspondences.Select(correspondence => (correspondence.Recipient, correspondence.ResourceId)).Distinct().ToList();
        XacmlJsonRequestRoot jsonRequest = CreateMultiDecisionRequestForLegacy(user, ssn, recipientWithResources);
        var responseContent = await AuthorizeRequest(jsonRequest, cancellationToken);

        var results = new Dictionary<(string, string), int?>();
        for (int i = 0; i < responseContent.Response.Count; i++)
        {
            var authorizationResponse = responseContent.Response[i];
            var recipientWithResource = recipientWithResources[i];
            if (authorizationResponse.Decision == "Permit")
            {
                var obligation = GetObligation("urn:altinn:minimum-authenticationlevel", authorizationResponse.Obligations);
                int? authLevel = int.Parse(obligation.Value);
                results.Add((recipientWithResource.Recipient, recipientWithResource.ResourceId), authLevel);
            } 
            else
            {
                results.Add((recipientWithResource.Recipient, recipientWithResource.ResourceId), null);
            }
        }
        return results;
    }

    private async Task<bool> CheckUserAccess(ClaimsPrincipal? user, string resourceId, string party, string? correspondenceId, List<ResourceAccessLevel> rights, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Checking access for party {party} and resource {resourceId}", party.SanitizeForLogging(), resourceId.SanitizeForLogging());
        if (user is null)
        {
            throw new InvalidOperationException("This operation cannot be called outside an authenticated HttpContext");
        }
        var bypassDecision = await EvaluateBypassConditions(user, resourceId, cancellationToken);
        if (bypassDecision is not null)
        {
            return bypassDecision.Value;
        }
        var actionIds = rights.Select(GetActionId).ToList();
        XacmlJsonRequestRoot jsonRequest = CreateDecisionRequest(user, resourceId, party, correspondenceId, actionIds);
        var responseContent = await AuthorizeRequest(jsonRequest, cancellationToken);
        var validationResult = ValidateAuthorizationResponse(responseContent, user);
        return validationResult;
    }

    private async Task<bool?> EvaluateBypassConditions(ClaimsPrincipal user, string resourceId, CancellationToken cancellationToken)
    {
        if (_httpClient.BaseAddress is null)
        {
            _logger.LogWarning("Authorization service disabled");
            return true;
        }

        // New bypass rule: Check if user has altinn:serviceowner scope and matches resource service owner
        var serviceOwnerScope = user.Claims.FirstOrDefault(c => c.Type == "scope" && c.Value.Split(' ').Contains("altinn:serviceowner"));
        if (serviceOwnerScope != null)
        {
            var consumerClaim = user.Claims.FirstOrDefault(c => c.Type == "consumer")?.Value;
            var consumerOrg = string.Empty;

            if (!string.IsNullOrWhiteSpace(consumerClaim))
            {
                try
                {
                    var consumerObject = JsonSerializer.Deserialize<TokenConsumer>(consumerClaim);
                    consumerOrg = consumerObject?.ID.WithoutPrefix() ?? string.Empty;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse consumer claim JSON");
                }
            }
            var serviceOwnerId = await _resourceRepository.GetServiceOwnerOrganizationNumber(resourceId, cancellationToken);

            if (!string.IsNullOrWhiteSpace(consumerOrg))
            {
                if (!string.IsNullOrWhiteSpace(serviceOwnerId) && consumerOrg.Equals(serviceOwnerId, StringComparison.OrdinalIgnoreCase))
                {
                    _logger.LogInformation("Bypass granted for service owner: {serviceOwner} accessing resource: {resourceId}",
                        consumerOrg.SanitizeForLogging(), resourceId.SanitizeForLogging());
                    return true; // Allow access without PDP call
                }
            }
        }

        var serviceOwnerName = await _resourceRepository.GetServiceOwnerNameOfResource(resourceId, cancellationToken);
        if (string.IsNullOrWhiteSpace(serviceOwnerName))
        {
            _logger.LogWarning("Service owner not found for resource");
            return false;
        }
        return null;
    }

    private async Task<XacmlJsonResponse> AuthorizeRequest(XacmlJsonRequestRoot jsonRequest, CancellationToken cancellationToken)
    {
        XacmlJsonMultiRequests xacmlJsonMultiRequests = new XacmlJsonMultiRequests();
        var response = await _httpClient.PostAsJsonAsync("authorization/api/v1/authorize", jsonRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Failure when calling authorization: {response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        }
        var responseContent = await response.Content.ReadFromJsonAsync<XacmlJsonResponse>(cancellationToken: cancellationToken);
        if (responseContent is null)
        {
            throw new HttpRequestException("Unexpected null or invalid json response from Authorization.");
        }
        return responseContent;
    }

    private XacmlJsonRequestRoot CreateDecisionRequest(ClaimsPrincipal user, string resourceId, string party, string? instanceId, List<string> actionTypes)
    {
        var personIdClaim = GetPersonIdClaim(user);
        if (personIdClaim is not null && personIdClaim.Issuer == _dialogportenSettings.Issuer)
        {
            return DialogTokenXacmlMapper.CreateDialogportenDecisionRequest(user, resourceId, party, instanceId);
        }
        else
        {
            return AltinnTokenXacmlMapper.CreateAltinnDecisionRequest(user, actionTypes, resourceId, party, instanceId);
        }
    }

    private XacmlJsonRequestRoot CreateDecisionRequestForLegacy(ClaimsPrincipal user, string ssn, List<string> actionTypes, string resourceId, string onBehalfOf)
    {
        var personIdClaim = GetPersonIdClaim(user);
        if (personIdClaim is null || personIdClaim.Issuer == $"{_altinnOptions.PlatformGatewayUrl.TrimEnd('/')}/authentication/api/v1/openid/")
        {
            return AltinnTokenXacmlMapper.CreateAltinnDecisionRequestForLegacy(user, ssn, actionTypes, resourceId, onBehalfOf);
        }
        throw new SecurityTokenInvalidIssuerException();
    }

    private XacmlJsonRequestRoot CreateMultiDecisionRequestForLegacy(ClaimsPrincipal user, string ssn, List<(string Recipient, string ResourceId)> recipientParties)
    {
        var personIdClaim = GetPersonIdClaim(user);
        if (personIdClaim is null || personIdClaim.Issuer == $"{_altinnOptions.PlatformGatewayUrl.TrimEnd('/')}/authentication/api/v1/openid/")
        {
            return AltinnTokenXacmlMapper.CreateMultiDecisionRequestForLegacy(user, ssn, recipientParties);
        }
        throw new SecurityTokenInvalidIssuerException();
    }


    private bool ValidateAuthorizationResponse(XacmlJsonResponse response, ClaimsPrincipal user)
    {
        if (response.Response == null || !response.Response.Any())
        {
            return false;
        }
        var personIdClaim = GetPersonIdClaim(user);
        if (personIdClaim?.Issuer == _idPortenSettings.Issuer)
        {
            return IdportenXacmlMapper.ValidateIdportenAuthorizationResponse(response, user);
        }
        if (personIdClaim?.Issuer == _dialogportenSettings.Issuer)
        {
            return DialogTokenXacmlMapper.ValidateDialogportenResult(response, user);
        }
        foreach (var decision in response.Response)
        {
            var result = DecisionHelper.ValidateDecisionResult(decision, user);
            if (result == false)
            {
                return false;
            }
        }
        return true;
    }

    private static string GetActionId(ResourceAccessLevel right)
    {
        return right switch
        {
            ResourceAccessLevel.Read => "read",
            ResourceAccessLevel.Write => "write",
            _ => throw new NotImplementedException()
        };
    }

    private Claim? GetPersonIdClaim(ClaimsPrincipal user)
    {
        var claim = user.Claims.FirstOrDefault(claim => claim.Type == "pid");
        if (claim is null)
        {
            claim = user.Claims.FirstOrDefault(claim => claim.Type == "p"); // OnBehalfOf
        }
        if (claim is null)
        {
            return null;
        }
        return claim;
    }
}
