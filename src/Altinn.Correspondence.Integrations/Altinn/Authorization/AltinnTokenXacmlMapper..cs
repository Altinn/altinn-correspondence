using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Common.PEP.Constants;
using Altinn.Common.PEP.Helpers;
using System.Security.Claims;
using System.Text.RegularExpressions;
using static Altinn.Authorization.ABAC.Constants.XacmlConstants;

namespace Altinn.Correspondence.Integrations.Altinn.Authorization;

public static class AltinnTokenXacmlMapper
{
    private const string DefaultIssuer = "Altinn";
    private const string DefaultType = "string";
    private const string OrgNumberAttributeId = "urn:altinn:organization:identifier-no";

    public static XacmlJsonRequestRoot CreateAltinnDecisionRequest(ClaimsPrincipal user, List<string> actionTypes, string resourceId)
    {
        XacmlJsonRequest request = new()
        {
            AccessSubject = new List<XacmlJsonCategory>(),
            Action = new List<XacmlJsonCategory>(),
            Resource = new List<XacmlJsonCategory>()
        };

        var subjectCategory = CreateSubjectCategory(user);
        request.AccessSubject.Add(subjectCategory);
        foreach (var actionType in actionTypes)
        {
            request.Action.Add(CreateActionCategory(actionType));
        }
        var resourceCategory = CreateResourceCategory(resourceId, user);
        request.Resource.Add(resourceCategory);

        XacmlJsonRequestRoot jsonRequest = new() { Request = request };
        return jsonRequest;
    }
    public static XacmlJsonRequestRoot CreateAltinnDecisionRequestForLegacy(ClaimsPrincipal user, string ssn, List<string> actionTypes, string resourceId, string recipient)
    {
        XacmlJsonRequest request = new()
        {
            AccessSubject = new List<XacmlJsonCategory>(),
            Action = new List<XacmlJsonCategory>(),
            Resource = new List<XacmlJsonCategory>()
        };

        var subjectCategory = CreateSubjectCategoryForLegacy(user, ssn);
        request.AccessSubject.Add(subjectCategory);
        foreach (var actionType in actionTypes)
        {
            request.Action.Add(CreateActionCategory(actionType));
        }
        var resourceCategory = CreateResourceCategory(resourceId, user, true, recipient);
        request.Resource.Add(resourceCategory);

        XacmlJsonRequestRoot jsonRequest = new() { Request = request };
        return jsonRequest;
    }

    private static XacmlJsonCategory CreateActionCategory(string actionType, bool includeResult = false)
    {
        XacmlJsonCategory actionAttributes = new()
        {
            Attribute = new List<XacmlJsonAttribute>
                {
                    DecisionHelper.CreateXacmlJsonAttribute(MatchAttributeIdentifiers.ActionId, actionType, DefaultType, DefaultIssuer, includeResult)
                }
        };
        return actionAttributes;
    }

    private static XacmlJsonCategory CreateResourceCategory(string resourceId, ClaimsPrincipal user, bool legacy = false, string? orgNr = null)
    {
        XacmlJsonCategory resourceCategory = new() { Attribute = new List<XacmlJsonAttribute>() };

        resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.ResourceId, resourceId, DefaultType, DefaultIssuer));
        var claim = user.Claims.FirstOrDefault(claim => IsClientOrgNo(claim.Type));
        if (legacy && orgNr is not null)
        {
            resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(OrgNumberAttributeId, orgNr, DefaultType, DefaultIssuer));
        }
        else if (claim is not null)
        {
            resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(OrgNumberAttributeId, claim.Value, DefaultType, DefaultIssuer));
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
                list.Add(CreateXacmlJsonAttribute("urn:altinn:organizationnumber", claim.Value, "string", claim.Issuer));
                list.Add(CreateXacmlJsonAttribute(OrgNumberAttributeId, claim.Value, "string", claim.Issuer));
            }
            else if (IsScopeClaim(claim.Type))
            {
                list.Add(CreateXacmlJsonAttribute("urn:scope", claim.Value, "string", claim.Issuer));
            }
            else if (IsJtiClaim(claim.Type))
            {
                list.Add(CreateXacmlJsonAttribute("urn:altinn:sessionid", claim.Value, "string", claim.Issuer));
            }
            else if (IsValidUrn(claim.Type))
            {
                list.Add(CreateXacmlJsonAttribute(claim.Type, claim.Value, "string", claim.Issuer));
            }
            else if (IsValidPid(claim.Type))
            {
                list.Add(CreateXacmlJsonAttribute("urn:altinn:person:identifier-no", claim.Value, "string", claim.Issuer));
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
            list.Add(CreateXacmlJsonAttribute("urn:altinn:person:identifier-no", ssn, "string", claim.Issuer));
            list.Add(CreateXacmlJsonAttribute("urn:scope", claim.Value, "string", claim.Issuer));
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

    private static XacmlJsonAttribute CreateXacmlJsonAttribute(string attributeId, string value, string dataType, string issuer, bool includeResult = false)
    {
        XacmlJsonAttribute xacmlJsonAttribute = new XacmlJsonAttribute();
        xacmlJsonAttribute.AttributeId = attributeId;
        xacmlJsonAttribute.Value = value;
        xacmlJsonAttribute.DataType = dataType;
        xacmlJsonAttribute.Issuer = issuer;
        xacmlJsonAttribute.IncludeInResult = includeResult;
        return xacmlJsonAttribute;
    }
}
