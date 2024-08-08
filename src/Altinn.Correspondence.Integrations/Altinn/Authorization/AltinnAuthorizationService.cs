using System.Net.Http.Json;
using System.Security.Claims;

using Altinn.Authorization.ABAC.Xacml;
using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Correspondence.Repositories;
using Altinn.Common.PEP.Helpers;
using Altinn.Correspondence.Core.Models.Enums;
using Altinn.Correspondence.Core.Options;
using Altinn.Correspondence.Core.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Altinn.Correspondence.Integrations.Altinn.Authorization;

public class AltinnAuthorizationService : IAltinnAuthorizationService
{
    private readonly HttpClient _httpClient;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IResourceRightsService _resourceRepository;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<AltinnAuthorizationService> _logger;

    public AltinnAuthorizationService(HttpClient httpClient, IOptions<AltinnOptions> altinnOptions, IHttpContextAccessor httpContextAccessor, IResourceRightsService resourceRepository, IHostEnvironment hostEnvironment, ILogger<AltinnAuthorizationService> logger)
    {
        httpClient.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", altinnOptions.Value.PlatformSubscriptionKey);
        _httpClient = httpClient;
        _httpContextAccessor = httpContextAccessor;
        _resourceRepository = resourceRepository;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public async Task<bool> CheckUserAccess(string resourceId, List<ResourceAccessLevel> rights, CancellationToken cancellationToken = default)
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
        var user = _httpContextAccessor.HttpContext?.User;
        if (user is null)
        {
            _logger.LogError("Unexpected null value. User was null when checking access to resource");
            return false;
        }
        var actionIds = rights.Select(GetActionId).ToList();
        XacmlJsonRequestRoot jsonRequest = CreateDecisionRequest(user, actionIds, resourceId);
        var response = await _httpClient.PostAsJsonAsync("authorization/api/v1/authorize", jsonRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return false;
        }
        var responseContent = await response.Content.ReadFromJsonAsync<XacmlJsonResponse>(cancellationToken: cancellationToken);
        if (responseContent is null)
        {
            _logger.LogError("Unexpected null or invalid json response from Authorization.");
            return false;
        }
        var validationResult = ValidateResult(responseContent);
        return validationResult;
    }

    private XacmlJsonRequestRoot CreateDecisionRequest(ClaimsPrincipal user, List<string> actionTypes, string resourceId)
    {
        XacmlJsonRequest request = new()
        {
            AccessSubject = new List<XacmlJsonCategory>(),
            Action = new List<XacmlJsonCategory>(),
            Resource = new List<XacmlJsonCategory>()
        };

        var subjectCategory = DecisionHelper.CreateSubjectCategory(user.Claims);
        request.AccessSubject.Add(subjectCategory);
        foreach (var actionType in actionTypes)
        {
            request.Action.Add(XacmlMappers.CreateActionCategory(actionType));
        }
        request.Resource.Add(XacmlMappers.CreateResourceCategory(resourceId));

        XacmlJsonRequestRoot jsonRequest = new() { Request = request };

        return jsonRequest;
    }

    private static bool ValidateResult(XacmlJsonResponse response)
    {
        if (response.Response[0].Decision.Equals(XacmlContextDecision.Permit.ToString()))
        {
            return true;
        }

        return false;
    }

    private string GetActionId(ResourceAccessLevel right)
    {
        return right switch
        {
            ResourceAccessLevel.See => "see",
            ResourceAccessLevel.Open => "open",
            ResourceAccessLevel.Send => "send",
            _ => throw new NotImplementedException()
        };
    }
}
