using Altinn.Authorization.ABAC.Xacml.JsonProfile;
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

    public static XacmlJsonRequestRoot CreateUserContactPointDecisionRequest(IReadOnlyList<int> userIds, int partyId, string resourceId)
    {
        XacmlJsonRequest request = new XacmlJsonRequest
        {
            AccessSubject = new List<XacmlJsonCategory>(),
            Action = new List<XacmlJsonCategory>(),
            Resource = new List<XacmlJsonCategory>(),
            MultiRequests = new XacmlJsonMultiRequests()
            {
                RequestReference = new List<XacmlJsonRequestReference>()
            }
        };

        var actionCategory = DecisionHelper.CreateActionCategory("read");
        actionCategory.Id = "a1";
        request.Action.Add(actionCategory);

        var resourceCategory = XacmlRequestFactory.CreateResourceCategory(resourceId, partyId.ToString(), null, DefaultIssuer);
        resourceCategory.Id = "r1";
        request.Resource.Add(resourceCategory);

        for (int i = 0; i < userIds.Count; i++)
        {
            var subjectCategory = new XacmlJsonCategory
            {
                Id = "s" + i,
                Attribute = [DecisionHelper.CreateXacmlJsonAttribute(UrnConstants.UserId, userIds[i].ToString(), DefaultType, DefaultIssuer)]
            };
            request.AccessSubject.Add(subjectCategory);
            request.MultiRequests.RequestReference.Add(new XacmlJsonRequestReference()
            {
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
}
