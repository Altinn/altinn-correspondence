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
    private readonly IAltinnRegisterService _altinnRegisterService;
    private readonly AltinnOptions _altinnOptions;
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
        _altinnOptions = altinnOptions.Value;
        _dialogportenSettings = dialogportenSettings.Value;
        _idPortenSettings = idPortenSettings.Value;
        _httpClient = httpClient;
        _resourceRepository = resourceRepository;
        _altinnRegisterService = altinnRegisterService;
        _logger = logger;
    }

    public Task<bool> CheckAccessAsSender(ClaimsPrincipal? user, string resourceId, string sender, string? instance, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("CheckAccessAsSender called with resourceId {resourceId} and sender {sender} and instance {instance}", resourceId.SanitizeForLogging(), sender.SanitizeForLogging(), instance.SanitizeForLogging());
        return CheckUserAccess(
            user,
            resourceId,
            sender,
            instance,
            new List<ResourceAccessLevel> { ResourceAccessLevel.Write },
            cancellationToken);
    }

    public Task<bool> CheckAccessAsSender(ClaimsPrincipal? user, CorrespondenceEntity correspondence, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("CheckAccessAsSender called for correspondence {correspondenceId} with resourceId {resourceId} and sender {sender}", correspondence.Id, correspondence.ResourceId.SanitizeForLogging(), correspondence.Sender.SanitizeForLogging());
        return CheckUserAccess(
            user,
            correspondence.ResourceId,
            correspondence.Sender,
            correspondence.Id.ToString(),
            new List<ResourceAccessLevel> { ResourceAccessLevel.Write },
            cancellationToken);
    }

    public Task<bool> CheckAccessAsRecipient(ClaimsPrincipal? user, CorrespondenceEntity correspondence, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("CheckAccessAsRecipient called for correspondence {correspondenceId} with resourceId {resourceId} and recipient {recipient}", correspondence.Id, correspondence.ResourceId.SanitizeForLogging(), correspondence.Recipient.SanitizeForLogging());
        return CheckUserAccess(
            user,
            correspondence.ResourceId,
            correspondence.Recipient,
            correspondence.Id.ToString(),
            new List<ResourceAccessLevel> { ResourceAccessLevel.Read },
            cancellationToken);
    }

    public Task<bool> CheckAttachmentAccessAsRecipient(ClaimsPrincipal? user, CorrespondenceEntity correspondence, AttachmentEntity attachment, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("CheckAttachmentAccessAsRecipient called for correspondence {correspondenceId} with attachment resourceId {attachmentResourceId} and recipient {recipient}", correspondence.Id, attachment.ResourceId.SanitizeForLogging(), correspondence.Recipient.SanitizeForLogging());
        return CheckUserAccess(
            user,
            attachment.ResourceId,
            correspondence.Recipient,
            correspondence.Id.ToString(),
            new List<ResourceAccessLevel> { ResourceAccessLevel.Read },
            cancellationToken);
    }

    public Task<bool> CheckAccessAsAny(ClaimsPrincipal? user, string resource, string party, CancellationToken cancellationToken)
    {
        _logger.LogDebug("CheckAccessAsAny called with resource {resource} and party {party}", resource.SanitizeForLogging(), party.SanitizeForLogging());
        return CheckUserAccess(
            user,
            resource,
            party,
            null,
            new List<ResourceAccessLevel> { ResourceAccessLevel.Read, ResourceAccessLevel.Write },
            cancellationToken);
    }


    public async Task<int?> CheckUserAccessAndGetMinimumAuthLevel(ClaimsPrincipal? user, string ssn, string resourceId, List<ResourceAccessLevel> rights, string onBehalfOf, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("CheckUserAccessAndGetMinimumAuthLevel called for resourceId {resourceId} with rights {@rights} and onBehalfOf {onBehalfOf}", resourceId.SanitizeForLogging(), rights, onBehalfOf.SanitizeForLogging());
        if (user is null)
        {
            throw new InvalidOperationException("This operation cannot be called outside an authenticated HttpContext");
        }
        var bypassDecision = await EvaluateBypassConditions(user, resourceId, cancellationToken);
        if (bypassDecision is not null)
        {
            _logger.LogDebug("Bypass decision evaluated in CheckUserAccessAndGetMinimumAuthLevel: {bypassDecision}", bypassDecision);
            return bypassDecision.Value ? 3 : null;
        }
        var actionIds = rights.Select(GetActionId).ToList();
        XacmlJsonRequestRoot jsonRequest = CreateDecisionRequestForLegacy(user, ssn, actionIds, resourceId, onBehalfOf);
        _logger.LogDebug("Authorization request for minimum auth level created for resourceId {resourceId} with actionIds {@actionIds}", resourceId.SanitizeForLogging(), actionIds);
        var responseContent = await AuthorizeRequest(jsonRequest, cancellationToken);
        _logger.LogDebug("Authorization response received for minimum auth level check for resourceId {resourceId}", resourceId.SanitizeForLogging());
        var validationResult = ValidateAuthorizationResponse(responseContent, user);
        if (!validationResult)
        {
            _logger.LogDebug("Authorization response validation failed for minimum auth level check for resourceId {resourceId}", resourceId.SanitizeForLogging());
            return null;
        }
        int? minLevel = IdportenXacmlMapper.GetMinimumAuthLevel(responseContent, user);
        _logger.LogDebug("Minimum auth level resolved to {minLevel} for resourceId {resourceId}", minLevel, resourceId.SanitizeForLogging());
        return minLevel;
    }

    private static XacmlJsonAttributeAssignment? GetObligation(string category, List<XacmlJsonObligationOrAdvice> obligations)
    {
        foreach (XacmlJsonObligationOrAdvice obligation in obligations)
        {
            var assignment = obligation.AttributeAssignment.FirstOrDefault(a => a.Category.Equals(category));
            if (assignment != null)
            {
                return assignment;
            }
        }

        return null;
    }
    public async Task<Dictionary<(string, string), int?>> CheckUserAccessAndGetMinimumAuthLevelWithMultirequest(ClaimsPrincipal? user, string ssn, List<CorrespondenceEntity> correspondences, CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("CheckUserAccessAndGetMinimumAuthLevelWithMultirequest called for {count} correspondences", correspondences.Count);
        if (user is null)
        {
            throw new InvalidOperationException("This operation cannot be called outside an authenticated HttpContext");
        }
        if (correspondences.Count == 0)
        {
            _logger.LogDebug("No correspondences provided to CheckUserAccessAndGetMinimumAuthLevelWithMultirequest, returning empty result");
            return new Dictionary<(string, string), int?>();
        }

        List<(string Recipient, string ResourceId)> recipientWithResources = correspondences.Select(correspondence => (correspondence.Recipient, correspondence.ResourceId)).Distinct().ToList();
        _logger.LogDebug("Multi-request prepared with {count} unique recipient/resource combinations", recipientWithResources.Count);
        XacmlJsonRequestRoot jsonRequest = CreateMultiDecisionRequestForLegacy(user, ssn, recipientWithResources);
        var responseContent = await AuthorizeRequest(jsonRequest, cancellationToken);
        _logger.LogDebug("Authorization response received for multi-request with {responseCount} decisions", responseContent.Response.Count);
        if (responseContent.Response.Count != recipientWithResources.Count)
        {
            _logger.LogError("Authorization response count mismatch. Expected: {Expected}, Received: {Received}",
                recipientWithResources.Count, responseContent.Response.Count);
            throw new InvalidOperationException($"Authorization service returned {responseContent.Response.Count} decisions but {recipientWithResources.Count} were requested");
        }
        var results = new Dictionary<(string, string), int?>();
        for (int i = 0; i < responseContent.Response.Count; i++)
        {
            var authorizationResponse = responseContent.Response[i];
            var recipientWithResource = recipientWithResources[i];
            if (authorizationResponse.Decision == "Permit")
            {
                var obligation = GetObligation("urn:altinn:minimum-authenticationlevel", authorizationResponse.Obligations);
                int? authLevel = obligation is not null ? int.Parse(obligation.Value) : null;
                _logger.LogDebug("Permit decision for recipient {recipient} and resource {resourceId} with minimum auth level {authLevel}", recipientWithResource.Recipient.SanitizeForLogging(), recipientWithResource.ResourceId.SanitizeForLogging(), authLevel);
                results.Add((recipientWithResource.Recipient, recipientWithResource.ResourceId), authLevel);
            }
            else
            {
                _logger.LogDebug("Non-permit decision for recipient {recipient} and resource {resourceId}", recipientWithResource.Recipient.SanitizeForLogging(), recipientWithResource.ResourceId.SanitizeForLogging());
                results.Add((recipientWithResource.Recipient, recipientWithResource.ResourceId), null);
            }
        }
        return results;
    }

    private async Task<bool> CheckUserAccess(ClaimsPrincipal? user, string resourceId, string party, string? correspondenceId, List<ResourceAccessLevel> rights, CancellationToken cancellationToken = default)
    {
        resourceId = resourceId.WithoutPrefix();
        _logger.LogDebug("CheckUserAccess called for party {party}, resourceId {resourceId}, correspondenceId {correspondenceId} and rights {@rights}", party.SanitizeForLogging(), resourceId.SanitizeForLogging(), correspondenceId.SanitizeForLogging(), rights);
        if (user is null)
        {
            throw new InvalidOperationException("This operation cannot be called outside an authenticated HttpContext");
        }
        var bypassDecision = await EvaluateBypassConditions(user, resourceId, cancellationToken);
        if (bypassDecision is not null)
        {
            _logger.LogDebug("Bypass decision evaluated in CheckUserAccess: {bypassDecision}", bypassDecision);
            return bypassDecision.Value;
        }
        var actionIds = rights.Select(GetActionId).ToList();
        _logger.LogDebug("Action ids resolved in CheckUserAccess: {@actionIds}", actionIds);
        XacmlJsonRequestRoot jsonRequest = await CreateDecisionRequest(user, resourceId, party, correspondenceId, actionIds, cancellationToken);
        _logger.LogDebug("Authorization request created in CheckUserAccess for resourceId {resourceId} and party {party}", resourceId.SanitizeForLogging(), party.SanitizeForLogging());
        var responseContent = await AuthorizeRequest(jsonRequest, cancellationToken);
        _logger.LogDebug("Authorization response received in CheckUserAccess for resourceId {resourceId} and party {party}", resourceId.SanitizeForLogging(), party.SanitizeForLogging());
        var validationResult = ValidateAuthorizationResponse(responseContent, user);
        _logger.LogDebug("Authorization response validation result in CheckUserAccess for resourceId {resourceId} and party {party}: {validationResult}", resourceId.SanitizeForLogging(), party.SanitizeForLogging(), validationResult);
        return validationResult;
    }

    private async Task<bool?> EvaluateBypassConditions(ClaimsPrincipal user, string resourceId, CancellationToken cancellationToken)
    {
        _logger.LogDebug("EvaluateBypassConditions called for resourceId {resourceId}", resourceId.SanitizeForLogging());
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
                    _logger.LogDebug("Parsed consumer claim with org {consumerOrg}", consumerOrg.SanitizeForLogging());
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to parse consumer claim JSON");
                }
            }
            var serviceOwnerId = await _resourceRepository.GetServiceOwnerOrganizationNumber(resourceId, cancellationToken);
            _logger.LogDebug("Service owner org id {serviceOwnerId} resolved for resourceId {resourceId}", serviceOwnerId.SanitizeForLogging(), resourceId.SanitizeForLogging());

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
        _logger.LogDebug("Bypass conditions not met for resourceId {resourceId}, proceeding with PDP call", resourceId.SanitizeForLogging());
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

    private async Task<XacmlJsonRequestRoot> CreateDecisionRequest(ClaimsPrincipal user, string resourceId, string party, string? instanceId, List<string> actionTypes, CancellationToken cancellationToken)
    {
        var issuer = user.Claims.FirstOrDefault(c => c.Type == "iss")?.Value;
        _logger.LogDebug("CreateDecisionRequest called with issuer {issuer}, resourceId {resourceId}, party {party}, instanceId {instanceId} and actionTypes {@actionTypes}", issuer, resourceId.SanitizeForLogging(), party.SanitizeForLogging(), instanceId.SanitizeForLogging(), actionTypes);

        var resolvedParty = party;
        if (!party.IsPartyId())
        {
            var registerParty = await _altinnRegisterService.LookUpPartyById(resolvedParty, cancellationToken);
            if (registerParty is not null && registerParty.PartyId > 0)
            {
                resolvedParty = registerParty.PartyId.ToString();
                _logger.LogDebug("Party identifier {originalParty} resolved to partyId {resolvedParty}", party.SanitizeForLogging(), resolvedParty.SanitizeForLogging());
            }
            else
            {
                _logger.LogDebug("Party identifier {originalParty} could not be resolved to a partyId", party.SanitizeForLogging());
            }
        }

        if (issuer == _dialogportenSettings.Issuer)
        {
            _logger.LogDebug("Using Dialogporten decision request mapper for issuer {issuer}", issuer);
            return await DialogTokenXacmlMapper.CreateDialogportenDecisionRequest(user, _altinnRegisterService, resourceId, resolvedParty, instanceId, cancellationToken);
        }
        if (issuer == _idPortenSettings.Issuer)
        {
            _logger.LogDebug("Using IdPorten decision request mapper for issuer {issuer}", issuer);
            return await IdportenXacmlMapper.CreateIdPortenDecisionRequest(user, _altinnRegisterService, actionTypes, resourceId, resolvedParty, instanceId, cancellationToken);
        }

        _logger.LogDebug("Using Altinn token decision request mapper for issuer {issuer}", issuer);
        return AltinnTokenXacmlMapper.CreateAltinnDecisionRequest(user, actionTypes, resourceId, resolvedParty, instanceId);
    }

    private XacmlJsonRequestRoot CreateDecisionRequestForLegacy(ClaimsPrincipal user, string ssn, List<string> actionTypes, string resourceId, string onBehalfOf)
    {
        _logger.LogDebug("CreateDecisionRequestForLegacy called for resourceId {resourceId} with onBehalfOf {onBehalfOf} and actionTypes {@actionTypes}", resourceId.SanitizeForLogging(), onBehalfOf.SanitizeForLogging(), actionTypes);
        var personIdClaim = GetPersonIdClaim(user);
        if (personIdClaim is null || personIdClaim.Issuer == $"{_altinnOptions.PlatformGatewayUrl.TrimEnd('/')}/authentication/api/v1/openid/")
        {
            _logger.LogDebug("Using Altinn legacy decision request mapper in CreateDecisionRequestForLegacy");
            return AltinnTokenXacmlMapper.CreateAltinnDecisionRequestForLegacy(user, ssn, actionTypes, resourceId, onBehalfOf);
        }
        _logger.LogWarning("SecurityTokenInvalidIssuerException will be thrown in CreateDecisionRequestForLegacy due to unexpected personIdClaim issuer {issuer}", personIdClaim.Issuer);
        throw new SecurityTokenInvalidIssuerException();
    }

    private XacmlJsonRequestRoot CreateMultiDecisionRequestForLegacy(ClaimsPrincipal user, string ssn, List<(string Recipient, string ResourceId)> recipientParties)
    {
        _logger.LogDebug("CreateMultiDecisionRequestForLegacy called with {count} recipient/resource combinations", recipientParties.Count);
        var personIdClaim = GetPersonIdClaim(user);
        if (personIdClaim is null || personIdClaim.Issuer == $"{_altinnOptions.PlatformGatewayUrl.TrimEnd('/')}/authentication/api/v1/openid/")
        {
            _logger.LogDebug("Using Altinn legacy multi-decision request mapper in CreateMultiDecisionRequestForLegacy");
            return AltinnTokenXacmlMapper.CreateMultiDecisionRequestForLegacy(user, ssn, recipientParties);
        }
        _logger.LogWarning("SecurityTokenInvalidIssuerException will be thrown in CreateMultiDecisionRequestForLegacy due to unexpected personIdClaim issuer {issuer}", personIdClaim.Issuer);
        throw new SecurityTokenInvalidIssuerException();
    }

    private bool ValidateAuthorizationResponse(XacmlJsonResponse response, ClaimsPrincipal user)
    {
        _logger.LogDebug("ValidateAuthorizationResponse called with {decisionCount} decisions", response.Response?.Count ?? 0);
        if (response.Response == null || !response.Response.Any())
        {
            _logger.LogDebug("ValidateAuthorizationResponse found no decisions in response");
            return false;
        }
        var issuer = user.Claims.FirstOrDefault(c => c.Type == "iss")?.Value;
        if (issuer == _idPortenSettings.Issuer)
        {
            _logger.LogDebug("Delegating response validation to IdPorten mapper in ValidateAuthorizationResponse");
            return IdportenXacmlMapper.ValidateIdportenAuthorizationResponse(response, user);
        }
        if (issuer == _dialogportenSettings.Issuer)
        {
            _logger.LogDebug("Delegating response validation to Dialogporten mapper in ValidateAuthorizationResponse");
            return DialogTokenXacmlMapper.ValidateDialogportenResult(response, user);
        }
        foreach (var decision in response.Response)
        {
            var result = DecisionHelper.ValidateDecisionResult(decision, user);
            if (result == false)
            {
                _logger.LogDebug("DecisionHelper validation failed for one of the decisions in ValidateAuthorizationResponse");
                return false;
            }
        }
        _logger.LogDebug("ValidateAuthorizationResponse succeeded for all decisions");
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
