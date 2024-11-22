using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Common.PEP.Helpers;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Integrations.Dialogporten.Mappers;
using Altinn.Correspondence.Integrations.Idporten;
using Altinn.Correspondence.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Net.Http.Json;
using System.Security.Claims;

namespace Altinn.Correspondence.Integrations.Altinn.Authorization;

public class AltinnAuthorizationService : IAltinnAuthorizationService
{
    private readonly HttpClient _httpClient;
    private readonly IResourceRightsService _resourceRepository;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly AltinnOptions _altinnOptions;
    private readonly DialogportenSettings _dialogportenSettings;
    private readonly IdportenSettings _idPortenSettings;
    private readonly ILogger<AltinnAuthorizationService> _logger;

    public AltinnAuthorizationService(HttpClient httpClient, IOptions<AltinnOptions> altinnOptions, IOptions<DialogportenSettings> dialogportenSettings, IOptions<IdportenSettings> idPortenSettings, IResourceRightsService resourceRepository, IHostEnvironment hostEnvironment, ILogger<AltinnAuthorizationService> logger)
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

    /// <summary>
    /// Checks if the user has access to the resource with any of the given rights
    /// </summary>
    public async Task<bool> CheckUserAccess(ClaimsPrincipal? user, string resourceId, string party, string? correspondenceId, List<ResourceAccessLevel> rights, CancellationToken cancellationToken = default)
    {
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
        if (responseContent is null) return false;

        var validationResult = ValidateAuthorizationResponse(responseContent, user);
        return validationResult;
    }


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
        if (responseContent is null) return null;

        var validationResult = ValidateAuthorizationResponse(responseContent, user);
        if (!validationResult)
        {
            return null;
        }
        int? minLevel = IdportenXacmlMapper.GetMinimumAuthLevel(responseContent, user);
        return minLevel;
    }

    public async Task<bool> CheckMigrationAccess(string resourceId, List<ResourceAccessLevel> rights, CancellationToken cancellationToken = default)
    {
        if (_hostEnvironment.IsDevelopment())
        {
            return true;
        }
        var serviceOwnerId = await _resourceRepository.GetServiceOwnerOfResource(resourceId, cancellationToken);
        if (string.IsNullOrWhiteSpace(serviceOwnerId))
        {
            return false;
        }

        return true;
    }
    private async Task<bool?> EvaluateBypassConditions(ClaimsPrincipal? user, string resourceId, CancellationToken cancellationToken)
    {
        if (_httpClient.BaseAddress is null)
        {
            _logger.LogWarning("Authorization service disabled");
            return true;
        }
        var serviceOwnerId = await _resourceRepository.GetServiceOwnerOfResource(resourceId, cancellationToken);
        if (string.IsNullOrWhiteSpace(serviceOwnerId))
        {
            _logger.LogWarning("Service owner not found for resource");
            return false;
        }
        return null;
    }

    private async Task<XacmlJsonResponse?> AuthorizeRequest(XacmlJsonRequestRoot jsonRequest, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync("authorization/api/v1/authorize", jsonRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }
        var responseContent = await response.Content.ReadFromJsonAsync<XacmlJsonResponse>(cancellationToken: cancellationToken);
        if (responseContent is null)
        {
            _logger.LogError("Unexpected null or invalid json response from Authorization.");
            return null;
        }
        return responseContent;
    }

    private XacmlJsonRequestRoot CreateDecisionRequest(ClaimsPrincipal user, string resourceId, string party, string? instanceId, List<string> actionTypes)
    {
        var personIdClaim = GetPersonIdClaim(user);
        if (personIdClaim is null || personIdClaim.Issuer == $"{_altinnOptions.PlatformGatewayUrl.TrimEnd('/')}/authentication/api/v1/openid/")
        {
            return AltinnTokenXacmlMapper.CreateAltinnDecisionRequest(user, actionTypes, resourceId, party, instanceId);
        }
        if (personIdClaim.Issuer == _dialogportenSettings.Issuer)
        {
            return DialogTokenXacmlMapper.CreateDialogportenDecisionRequest(user, resourceId, party);
        }
        if (personIdClaim.Issuer == _idPortenSettings.Issuer)
        {
            return IdportenXacmlMapper.CreateIdportenDecisionRequest(user, resourceId, actionTypes, party);
        }
        throw new SecurityTokenInvalidIssuerException();
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


    private bool ValidateAuthorizationResponse(XacmlJsonResponse response, ClaimsPrincipal user)
    {
        if (response.Response.IsNullOrEmpty())
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
            claim = user.Claims.FirstOrDefault(claim => claim.Type == "c");
        }
        if (claim is null)
        {
            return null;
        }
        return claim;
    }
}
