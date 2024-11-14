using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Common.PEP.Constants;
using Altinn.Common.PEP.Helpers;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace Altinn.Correspondence.Integrations.Altinn.Authorization;

public static class AltinnTokenXacmlMapper
{
    private const string DefaultIssuer = "Altinn";
    private const string DefaultType = "string";
    private const string OrgNumberAttributeId = "urn:altinn:organization:identifier-no";
    private const string PersonAttributeId = "urn:altinn:person:identifier-no";

    public static XacmlJsonRequestRoot CreateAltinnDecisionRequest(ClaimsPrincipal user, List<string> actionTypes, string resourceId, string? onBehalfOfIdentifier, string? correspondenceId)
    {
        XacmlJsonRequest request = new XacmlJsonRequest();
        request.AccessSubject = new List<XacmlJsonCategory>();
        request.Action = new List<XacmlJsonCategory>();
        request.Resource = new List<XacmlJsonCategory>();

        request.AccessSubject.Add(CreateSubjectCategory(user));
        request.Action.AddRange(actionTypes.Select(action => DecisionHelper.CreateActionCategory(action)));
        request.Resource.Add(CreateResourceCategory(resourceId, user, onBehalfOfIdentifier, correspondenceId));

        XacmlJsonRequestRoot jsonRequest = new() { Request = request };

        return jsonRequest;
    }
    public static XacmlJsonRequestRoot CreateAltinnDecisionRequestForLegacy(ClaimsPrincipal user, string ssn, List<string> actionTypes, string resourceId, string onBehalfOfIdentifier)
    {
        XacmlJsonRequest request = new XacmlJsonRequest();
        request.AccessSubject = new List<XacmlJsonCategory>();
        request.Action = new List<XacmlJsonCategory>();
        request.Resource = new List<XacmlJsonCategory>();

        request.AccessSubject.Add(CreateSubjectCategoryForLegacy(user, ssn));
        request.Action.AddRange(actionTypes.Select(action => DecisionHelper.CreateActionCategory(action)));
        request.Resource.Add(CreateResourceCategory(resourceId, user, onBehalfOfIdentifier));
    
        XacmlJsonRequestRoot jsonRequest = new() { Request = request };

        return jsonRequest;
    }


    private static XacmlJsonCategory CreateResourceCategory(string resourceId, ClaimsPrincipal user, string? onBehalfOfIdentifier = null, string? correspondenceId = null)
    {
        XacmlJsonCategory resourceCategory = new() { Attribute = new List<XacmlJsonAttribute>() };

        resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.ResourceId, resourceId, DefaultType, DefaultIssuer));
        var claim = user.Claims.FirstOrDefault(claim => IsClientOrgNo(claim.Type));
        if (onBehalfOfIdentifier is not null)
        {
            resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(OrgNumberAttributeId, onBehalfOfIdentifier, DefaultType, DefaultIssuer));
        }
        else if (claim is not null)
        {
            resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(OrgNumberAttributeId, claim.Value, DefaultType, DefaultIssuer));
        }
        if (correspondenceId is not null)
        {
            resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.ResourceInstance, correspondenceId, DefaultType, DefaultIssuer));
        }
        return resourceCategory;
    }

    private static XacmlJsonCategory CreateSubjectCategory(ClaimsPrincipal user)
    {
        XacmlJsonCategory xacmlJsonCategory = new XacmlJsonCategory();
        List<XacmlJsonAttribute> list = new List<XacmlJsonAttribute>();

        foreach (Claim claim in user.Claims)
        {
            if (IsCamelCaseOrgnumberClaim(claim.Type))
            {
                list.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.OrganizationNumber, claim.Value, DefaultType, claim.Issuer));
                list.Add(DecisionHelper.CreateXacmlJsonAttribute(OrgNumberAttributeId, claim.Value, DefaultType, claim.Issuer));
            }
            else if (IsScopeClaim(claim.Type))
            {
                list.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.Scope, claim.Value, DefaultType, claim.Issuer));
            }
            else if (IsJtiClaim(claim.Type))
            {
                list.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.SessionId, claim.Value, DefaultType, claim.Issuer));
            }
            else if (IsValidUrn(claim.Type))
            {
                list.Add(DecisionHelper.CreateXacmlJsonAttribute(claim.Type, claim.Value, DefaultType, claim.Issuer));
            }
            else if (IsValidPid(claim.Type))
            {
                list.Add(DecisionHelper.CreateXacmlJsonAttribute(PersonAttributeId, claim.Value, DefaultType, claim.Issuer));
            }
        }
        xacmlJsonCategory.Attribute = list;
        return xacmlJsonCategory;
    }
    private static XacmlJsonCategory CreateSubjectCategoryForLegacy(ClaimsPrincipal user, string ssn)
    {
        XacmlJsonCategory xacmlJsonCategory = new XacmlJsonCategory();
        List<XacmlJsonAttribute> list = new List<XacmlJsonAttribute>();
        var claim = user.Claims.FirstOrDefault(claim => IsScopeClaim(claim.Type));
        if (claim is not null)
        {
            list.Add(DecisionHelper.CreateXacmlJsonAttribute(PersonAttributeId, ssn, DefaultType, claim.Issuer));
            list.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.Scope, claim.Value, DefaultType, claim.Issuer));
        }
        xacmlJsonCategory.Attribute = list;
        return xacmlJsonCategory;
    }
    private static bool IsValidUrn(string value)
    {
        Regex regex = new Regex("^urn*");
        return regex.Match(value).Success;
    }

    private static bool IsCamelCaseOrgnumberClaim(string value)
    {
        return value.Equals("urn:altinn:orgNumber");
    }

    private static bool IsClientOrgNo(string value)
    {
        return value.Equals("client_orgno");
    }

    private static bool IsScopeClaim(string value)
    {
        return value.Equals("scope");
    }

    private static bool IsJtiClaim(string value)
    {
        return value.Equals("jti");
    }

    private static bool IsValidPid(string value)
    {
        return value.Equals("pid");
    }
}
