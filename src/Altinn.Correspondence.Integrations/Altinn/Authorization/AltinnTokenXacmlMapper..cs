using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Common.PEP.Constants;
using Altinn.Common.PEP.Helpers;
using Altinn.Correspondence.Common.Helpers;
using System.Security.Claims;

namespace Altinn.Correspondence.Integrations.Altinn.Authorization;

public static class AltinnTokenXacmlMapper
{
    private const string DefaultIssuer = "Altinn";
    private const string DefaultType = "string";
    private const string PersonAttributeId = "urn:altinn:person:identifier-no";

    public static XacmlJsonRequestRoot CreateAltinnDecisionRequest(ClaimsPrincipal user, List<string> actionTypes, string resourceId, string party, string? instance)
    {
        XacmlJsonRequest request = new XacmlJsonRequest();
        request.AccessSubject = new List<XacmlJsonCategory>();
        request.Action = new List<XacmlJsonCategory>();
        request.Resource = new List<XacmlJsonCategory>();

        request.AccessSubject.Add(CreateSubjectCategory(user));
        request.Action.AddRange(actionTypes.Select(action => DecisionHelper.CreateActionCategory(action)));
        request.Resource.Add(CreateResourceCategory(resourceId, user, party, instance));

        XacmlJsonRequestRoot jsonRequest = new() { Request = request };

        return jsonRequest;
    }
    public static XacmlJsonRequestRoot CreateAltinnDecisionRequestForLegacy(ClaimsPrincipal user, string ssn, List<string> actionTypes, string resourceId, string onBehalfOf)
    {
        XacmlJsonRequest request = new XacmlJsonRequest();
        request.AccessSubject = new List<XacmlJsonCategory>();
        request.Action = new List<XacmlJsonCategory>();
        request.Resource = new List<XacmlJsonCategory>();

        request.AccessSubject.Add(CreateSubjectCategoryForLegacy(user, ssn));
        request.Action.AddRange(actionTypes.Select(action => DecisionHelper.CreateActionCategory(action)));
        request.Resource.Add(CreateResourceCategory(resourceId, user, onBehalfOf));
    
        XacmlJsonRequestRoot jsonRequest = new() { Request = request };

        return jsonRequest;
    }


    private static XacmlJsonCategory CreateResourceCategory(string resourceId, ClaimsPrincipal user, string recipientId, string? correspondenceId = null)
    {
        XacmlJsonCategory resourceCategory = new() { Attribute = new List<XacmlJsonAttribute>() };

        resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.ResourceId, resourceId, DefaultType, DefaultIssuer));

        if (recipientId.IsOrganizationNumber())
        {
            resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.OrganizationNumberAttribute, recipientId.GetOrgNumberWithoutPrefix(), DefaultType, DefaultIssuer));
        }
        else if (recipientId.IsSocialSecurityNumber())
        {
            resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(PersonAttributeId, recipientId, DefaultType, DefaultIssuer));
        }
        else
        {
            throw new InvalidOperationException("RecipientId is not a valid organization or person number");
        }
        if (correspondenceId is not null)
        {
            resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.ResourceInstance, correspondenceId, DefaultType, DefaultIssuer));
        }
        return resourceCategory;
    }

    private static XacmlJsonCategory CreateSubjectCategory(ClaimsPrincipal user)
    {
        var subjectCategory = DecisionHelper.CreateSubjectCategory(user.Claims);
        var isSystemUserSubject = subjectCategory.Attribute.Any(attribute => attribute.AttributeId == AltinnXacmlUrns.SystemUserUuid);
        if (!isSystemUserSubject)
        {
            var pidClaim = user.Claims.FirstOrDefault(claim => IsValidPid(claim.Type));
            if (pidClaim is not null)
            {
                subjectCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(PersonAttributeId, pidClaim.Value, DefaultType, pidClaim.Issuer));
            }
        }
        return subjectCategory;
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

    private static bool IsScopeClaim(string value)
    {
        return value.Equals("scope");
    }

    private static bool IsValidPid(string value)
    {
        return value.Equals("pid");
    }
}
