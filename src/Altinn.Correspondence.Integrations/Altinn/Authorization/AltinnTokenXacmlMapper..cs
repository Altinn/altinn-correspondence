using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Common.PEP.Constants;
using Altinn.Common.PEP.Helpers;
using Altinn.Correspondence.Common.Constants;
using System.Security.Claims;

namespace Altinn.Correspondence.Integrations.Altinn.Authorization;

public static class AltinnTokenXacmlMapper
{
    private const string DefaultIssuer = "Altinn";
    private const string DefaultType = "string";

    public static XacmlJsonRequestRoot CreateAltinnDecisionRequest(ClaimsPrincipal user, List<string> actionTypes, string resourceId, string party, string? instanceId)
    {
        XacmlJsonRequest request = new XacmlJsonRequest();
        request.AccessSubject = new List<XacmlJsonCategory>();
        request.Action = new List<XacmlJsonCategory>();
        request.Resource = new List<XacmlJsonCategory>();

        request.AccessSubject.Add(CreateSubjectCategory(user));
        request.Action.AddRange(actionTypes.Select(action => DecisionHelper.CreateActionCategory(action)));
        request.Resource.Add(XacmlRequestFactory.CreateResourceCategory(resourceId, party, instanceId, DefaultIssuer));

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
        request.Resource.Add(XacmlRequestFactory.CreateResourceCategory(resourceId, onBehalfOf, null, DefaultIssuer));
    
        XacmlJsonRequestRoot jsonRequest = new() { Request = request };

        return jsonRequest;
    }

    public static XacmlJsonRequestRoot CreateMultiDecisionRequestForLegacy(ClaimsPrincipal user, string ssn, List<(string Recipient, string ResourceId)> recipientParties)
    {
        XacmlJsonRequest request = new XacmlJsonRequest();
        request.AccessSubject = new List<XacmlJsonCategory>();
        request.Action = new List<XacmlJsonCategory>();
        request.Resource = new List<XacmlJsonCategory>();

        var subjectCategory = CreateSubjectCategoryForLegacy(user, ssn);
        subjectCategory.Id = "s1";
        request.AccessSubject.Add(subjectCategory);
        var actionCategory = DecisionHelper.CreateActionCategory("read");
        actionCategory.Id = "a1";
        request.Action.Add(actionCategory);
        request.MultiRequests = new XacmlJsonMultiRequests()
        {
            RequestReference = new List<XacmlJsonRequestReference>()
        };
        foreach (var recipientParty in recipientParties)
        {
            var resourceCategory = XacmlRequestFactory.CreateResourceCategory(recipientParty.ResourceId, recipientParty.Recipient, null, DefaultIssuer);
            resourceCategory.Id = recipientParty.Recipient + "::" + recipientParty.ResourceId;
            request.Resource.Add(resourceCategory);
            request.MultiRequests.RequestReference.Add(new XacmlJsonRequestReference(){
                ReferenceId = [subjectCategory.Id, actionCategory.Id, resourceCategory.Id]
            });
        }
        XacmlJsonRequestRoot jsonRequest = new() { Request = request };

        return jsonRequest;
    }


    private static XacmlJsonCategory CreateSubjectCategory(ClaimsPrincipal user)
    {
        var subjectCategory = DecisionHelper.CreateSubjectCategory(user.Claims);
        return subjectCategory;
    }

    private static XacmlJsonCategory CreateSubjectCategoryForLegacy(ClaimsPrincipal user, string ssn)
    {
        XacmlJsonCategory xacmlJsonCategory = new XacmlJsonCategory();
        List<XacmlJsonAttribute> list = new List<XacmlJsonAttribute>();
        var claim = user.Claims.FirstOrDefault(claim => IsScopeClaim(claim.Type));
        if (claim is not null)
        {
            list.Add(DecisionHelper.CreateXacmlJsonAttribute(UrnConstants.PersonIdAttribute, ssn, DefaultType, claim.Issuer));
            list.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.Scope, claim.Value, DefaultType, claim.Issuer));
        }
        xacmlJsonCategory.Attribute = list;
        return xacmlJsonCategory;
    }

    private static bool IsScopeClaim(string value)
    {
        return value.Equals("scope");
    }
}
