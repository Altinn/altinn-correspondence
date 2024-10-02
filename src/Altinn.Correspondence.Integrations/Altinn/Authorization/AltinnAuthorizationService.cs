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
using Azure.Core;
using System.Diagnostics;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Diagnostics.CodeAnalysis;
using Altinn.Platform.Register.Models;
using Npgsql.Internal;
using System;
using System.IO;
using System.Text.Json;
using Altinn.Correspondence.Core.Models.Entities;

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

    public async Task<List<SimpleParty>> GetAutorizedParties(SimpleParty partyToRequestFor, CancellationToken cancellationToken = default)
    {
        AuthorizedPartiesRequest request = new(partyToRequestFor);
        JsonSerializerOptions serializerOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault
        };
        var response = await _httpClient.PostAsJsonAsync("/accessmanagement/api/v1/resourceowner/authorizedparties?includeAltinn2=true", request, serializerOptions, cancellationToken: cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Error code in call against Authorization GetAuthorizedParties");
            return null;
        }
        var responseContent = await response.Content.ReadFromJsonAsync<XacmlJsonResponse>(cancellationToken: cancellationToken);
        if (responseContent is null)
        {
            _logger.LogError("Unexpected null or invalid json response from Authorization GetAuthorizedParties.");
            return null;
        }
        
        return null;
    }
}

internal sealed class AuthorizedPartiesRequest
{
    public string Type { get; init; }
    public string Value { get; init; }

    public AuthorizedPartiesRequest(SimpleParty party)
    {
        if(party.PartyTypeName == SimplePartyType.Person)
        {
            Type = "urn:altinn:person:identifier-no";
            Value = party.SSN;
        }
        else
        {
            Type = "urn:altinn:organization:identifier-no";
            Value = party.OrgNumber;
        }
    }
}