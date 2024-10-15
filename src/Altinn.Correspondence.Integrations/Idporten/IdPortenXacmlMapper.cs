using Altinn.Authorization.ABAC.Xacml;
using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Common.PEP.Constants;
using Altinn.Common.PEP.Helpers;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using static Altinn.Authorization.ABAC.Constants.XacmlConstants;

namespace Altinn.Correspondence.Integrations.Idporten
{
    public static class IdportenXacmlMapper
    {
        private const string DefaultIssuer = "Idporten";
        private const string DefaultType = "string";

        public static XacmlJsonRequestRoot CreateIdportenDecisionRequest(ClaimsPrincipal user, string resourceId, List<string> actionTypes, string recipientOrgNo)
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
            var resourceCategory = CreateResourceCategory(resourceId, recipientOrgNo);
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

        private static XacmlJsonCategory CreateResourceCategory(string resourceId, string recipientOrgNo)
        {
            XacmlJsonCategory resourceCategory = new() { Attribute = new List<XacmlJsonAttribute>() };
            resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.ResourceId, resourceId, DefaultType, DefaultIssuer));
            resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute("urn:altinn:organization:identifier-no", recipientOrgNo, DefaultType, DefaultIssuer));
            return resourceCategory;
        }

        private static XacmlJsonCategory CreateSubjectCategory(ClaimsPrincipal user)
        {
            XacmlJsonCategory xacmlJsonCategory = new XacmlJsonCategory();
            List<XacmlJsonAttribute> list = new List<XacmlJsonAttribute>();

            foreach (Claim claim in user.Claims)
            {
                if (IsSsnClaim(claim.Type))
                {
                    list.Add(CreateXacmlJsonAttribute("urn:altinn:person:identifier-no", claim.Value, "string", claim.Issuer));
                }
            }
            xacmlJsonCategory.Attribute = list;
            return xacmlJsonCategory;
        }

        private static bool IsSsnClaim(string value)
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

        public static bool ValidateIdportenAuthorizationResponse(XacmlJsonResponse response, ClaimsPrincipal user)
        {
            foreach (var result in response.Response)
            {
                if (!result.Decision.Equals(XacmlContextDecision.Permit.ToString()))
                {
                    return false;
                }

                if (result.Obligations != null)
                {
                    List<XacmlJsonObligationOrAdvice> obligations = result.Obligations;
                    XacmlJsonAttributeAssignment? obligation = GetObligation("urn:altinn:minimum-authenticationlevel", obligations);
                    if (obligation != null)
                    {
                        var minimumAuthLevel = Convert.ToInt32(obligation.Value);
                        var userAuthLevelClaim = user.Claims.FirstOrDefault((Claim c) => c.Type.Equals("http://schemas.microsoft.com/claims/authnclassreference"));
                        if (userAuthLevelClaim is null)
                        {
                            throw new SecurityTokenMalformedException();
                        }
                        if (userAuthLevelClaim.Value == "idporten-loa-high")
                        {
                            return minimumAuthLevel <= 4;
                        }
                        else if (userAuthLevelClaim.Value == "idporten-loa-substantial")
                        {
                            return minimumAuthLevel <= 3;
                        }
                        else if (userAuthLevelClaim.Value == "idporten-loa-low")
                        {
                            return minimumAuthLevel <= 2;
                        }
                        else
                        {
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        private static XacmlJsonAttributeAssignment? GetObligation(string category, List<XacmlJsonObligationOrAdvice> obligations)
        {
            foreach (XacmlJsonObligationOrAdvice obligation in obligations)
            {
                var xacmlJsonAttributeAssignment = obligation.AttributeAssignment.FirstOrDefault((XacmlJsonAttributeAssignment a) => a.Category.Equals(category));
                if (xacmlJsonAttributeAssignment != null)
                {
                    return xacmlJsonAttributeAssignment;
                }
            }
            return null;
        }
    }
}
