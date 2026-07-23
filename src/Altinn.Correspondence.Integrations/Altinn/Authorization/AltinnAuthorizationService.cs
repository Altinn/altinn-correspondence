using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Common.PEP.Helpers;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Common.Helpers.Models;
using Altinn.Correspondence.Core.Extensions;
using Altinn.Correspondence.Core.Models.Entities;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Repositories;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Integrations.Dialogporten.Mappers;
using Altinn.Correspondence.Integrations.Idporten;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;

namespace Altinn.Correspondence.Integrations.Altinn.Authorization;

public class AltinnAuthorizationService : IAltinnAuthorizationService
{
    private readonly HttpClient _httpClient;
    private readonly IResourceRegistryService _resourceRepository;
    private readonly IAltinnRegisterService _altinnRegisterService;
    private readonly DialogportenSettings _dialogportenSettings;
    private readonly IdportenSettings _idPortenSettings;
    private readonly ILogger<AltinnAuthorizationService> _logger;

    public AltinnAuthorizationService(
        HttpClient httpClient,
        IOptions<AltinnOptions> altinnOptions,
        IOptions<DialogportenSettings> dialogportenSettings,
        IOptions<IdportenSettings> idPortenSettings,
        IResourceRegistryService resourceRepository,
        IAltinnRegisterService altinnRegisterService,
        ILogger<AltinnAuthorizationService> logger)
    {
        httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", altinnOptions.Value.PlatformSubscriptionKey);
        _dialogportenSettings = dialogportenSettings.Value;
        _idPortenSettings = idPortenSettings.Value;
        _httpClient = httpClient;
        _resourceRepository = resourceRepository;
        _altinnRegisterService = altinnRegisterService;
        _logger = logger;
    }

    public Task<bool> CheckAccessAsSender(ClaimsPrincipal? user, string resourceId, string sender, string? instance, CancellationToken cancellationToken = default)
        => CheckUserAccess(
            user,
            resourceId,
            sender,
            instance,
            new List<ResourceAccessLevel> { ResourceAccessLevel.Write },
            cancellationToken);

    public Task<bool> CheckAccessAsSender(ClaimsPrincipal? user, CorrespondenceEntity correspondence, CancellationToken cancellationToken = default) =>
        CheckUserAccess(
            user,
            correspondence.ResourceId,
            correspondence.Sender,
            correspondence.Id.ToString(),
            new List<ResourceAccessLevel> { ResourceAccessLevel.Write },
            cancellationToken);

    public Task<bool> CheckAccessAsRecipient(ClaimsPrincipal? user, CorrespondenceEntity correspondence, CancellationToken cancellationToken = default) =>
        CheckUserAccess(
            user,
            correspondence.ResourceId,
            correspondence.Recipient,
            correspondence.Id.ToString(),
            new List<ResourceAccessLevel> { ResourceAccessLevel.Read },
            cancellationToken);

    public Task<bool> CheckAttachmentAccessAsRecipient(ClaimsPrincipal? user, CorrespondenceEntity correspondence, AttachmentEntity attachment, CancellationToken cancellationToken = default) =>
        CheckUserAccess(
            user,
            attachment.ResourceId,
            correspondence.Recipient,
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


    private async Task<bool> CheckUserAccess(ClaimsPrincipal? user, string resourceId, string party, string? correspondenceId, List<ResourceAccessLevel> rights, CancellationToken cancellationToken = default)
    {
        resourceId = resourceId.WithoutPrefix();
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
        XacmlJsonRequestRoot jsonRequest = await CreateDecisionRequest(user, resourceId, party, correspondenceId, actionIds, cancellationToken);
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
                    _logger.LogDebug("Bypass granted for service owner: {serviceOwner} accessing resource: {resourceId}",
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

    private async Task<XacmlJsonRequestRoot> CreateDecisionRequest(ClaimsPrincipal user, string resourceId, string party, string? correspondenceId, List<string> actionTypes, CancellationToken cancellationToken)
    {
        var issuer = user.Claims.FirstOrDefault(c => c.Type == "iss")?.Value;

        var resolvedParty = party;
        if (!party.IsPartyId())
        {
            var registerParty = await _altinnRegisterService.LookUpPartyById(resolvedParty, cancellationToken);
            if (registerParty is not null && registerParty.GetPartyId() > 0)
            {
                resolvedParty = registerParty.GetPartyId().ToString();
            }
        }

        if (!string.IsNullOrWhiteSpace(correspondenceId) && !correspondenceId.StartsWith("urn:altinn:correspondence-id:", StringComparison.Ordinal))
        {
            correspondenceId = "urn:altinn:correspondence-id:" + correspondenceId;
        }

        if (issuer == _dialogportenSettings.Issuer)
        {
            return await DialogTokenXacmlMapper.CreateDialogportenDecisionRequest(user, _altinnRegisterService, resourceId, resolvedParty, correspondenceId, cancellationToken);
        }
        if (issuer == _idPortenSettings.Issuer)
        {
            return await IdportenXacmlMapper.CreateIdPortenDecisionRequest(user, _altinnRegisterService, actionTypes, resourceId, resolvedParty, correspondenceId, cancellationToken);
        }

        return AltinnTokenXacmlMapper.CreateAltinnDecisionRequest(user, actionTypes, resourceId, resolvedParty, correspondenceId);
    }

    private bool ValidateAuthorizationResponse(XacmlJsonResponse response, ClaimsPrincipal user)
    {
        if (response.Response == null || !response.Response.Any())
        {
            return false;
        }
        var issuer = user.Claims.FirstOrDefault(c => c.Type == "iss")?.Value;
        if (issuer == _idPortenSettings.Issuer)
        {
            return IdportenXacmlMapper.ValidateIdportenAuthorizationResponse(response, user);
        }
        if (issuer == _dialogportenSettings.Issuer)
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
}
