using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Common.PEP.Constants;
using Altinn.Common.PEP.Helpers;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text.RegularExpressions;
using static Altinn.Authorization.ABAC.Constants.XacmlConstants;

namespace Altinn.Correspondence.Integrations.Dialogporten.Mappers
{
    public static class DialogTokenXacmlMapper
    {
        internal const string DefaultIssuer = "Dialogporten";
        internal const string DefaultType = "string";

        public static XacmlJsonRequestRoot CreateDialogportenDecisionRequest(ClaimsPrincipal user, string resourceId)
        {
            XacmlJsonRequest request = new()
            {
                AccessSubject = new List<XacmlJsonCategory>(),
                Action = new List<XacmlJsonCategory>(),
                Resource = new List<XacmlJsonCategory>()
            };

            var subjectCategory = CreateSubjectCategory(user);
            request.AccessSubject.Add(subjectCategory);
            request.Action.Add(CreateActionCategory(user));
            var resourceCategory = CreateResourceCategory(resourceId, user);
            request.Resource.Add(resourceCategory);

            XacmlJsonRequestRoot jsonRequest = new() { Request = request };

            return jsonRequest;
        }

        private static XacmlJsonCategory CreateActionCategory(ClaimsPrincipal user, bool includeResult = false)
        {
            var actionClaim = user.Claims.FirstOrDefault(claim => IsActionClaim(claim.Type));
            if (actionClaim is null)
            {
                throw new SecurityTokenException("Dialogporten token does not contain the required action claim");
            }
            XacmlJsonCategory actionAttributes = new()
            {
                Attribute = new List<XacmlJsonAttribute>
                {
                    DecisionHelper.CreateXacmlJsonAttribute(MatchAttributeIdentifiers.ActionId, actionClaim.Value, DefaultType, actionClaim.Issuer, includeResult)
                }
            };
            return actionAttributes;
        }

        private static XacmlJsonCategory CreateResourceCategory(string resourceId, ClaimsPrincipal user)
        {
            XacmlJsonCategory resourceCategory = new() { Attribute = new List<XacmlJsonAttribute>() };
            resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.ResourceId, resourceId, DefaultType, DefaultIssuer));
            var orgClaim = user.Claims.FirstOrDefault(claim => IsOrgClaim(claim.Type));
            if (orgClaim is not null)
            {
                resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute("urn:altinn:organization:identifier-no", orgClaim.Value, DefaultType, DefaultIssuer));
            }
            return resourceCategory;
        }

        private static XacmlJsonCategory CreateSubjectCategory(ClaimsPrincipal user)
        {
            XacmlJsonCategory xacmlJsonCategory = new XacmlJsonCategory();
            List<XacmlJsonAttribute> list = new List<XacmlJsonAttribute>();

            foreach (Claim claim in user.Claims)
            {
                if (IsOrgClaim(claim.Type))
                {
                    list.Add(CreateXacmlJsonAttribute("urn:altinn:organizationnumber", claim.Value, "string", claim.Issuer));
                    list.Add(CreateXacmlJsonAttribute("urn:altinn:organization:identifier-no", claim.Value, "string", claim.Issuer));
                }
                else if (IsActionClaim(claim.Type))
                {
                    if (claim.Value == "read")
                    {
                        list.Add(CreateXacmlJsonAttribute("urn:scope", "altinn:correspondence.read", "string", claim.Issuer));
                    }
                    else if (claim.Value == "write")
                    {
                        list.Add(CreateXacmlJsonAttribute("urn:scope", "altinn:correspondence.write", "string", claim.Issuer));
                    }
                }
                else if (IsJtiClaim(claim.Type))
                {
                    list.Add(CreateXacmlJsonAttribute("urn:altinn:sessionid", claim.Value, "string", claim.Issuer));
                }
                else if (IsValidUrn(claim.Type))
                {
                    list.Add(CreateXacmlJsonAttribute(claim.Type, claim.Value, "string", claim.Issuer));
                }
                else if (IsSsnClaim(claim.Type))
                {
                    list.Add(CreateXacmlJsonAttribute("urn:altinn:person:identifier-no", claim.Value, "string", claim.Issuer));
                }
            }
            xacmlJsonCategory.Attribute = list;
            return xacmlJsonCategory;
        }
        private static bool IsValidUrn(string value)
        {
            Regex regex = new Regex("^urn*");
            return regex.Match(value).Success;
        }

        private static bool IsOrgClaim(string value)
        {
            return value.Equals("p");
        }
        private static bool IsSsnClaim(string value)
        {
            return value.Equals("c");
        }

        private static bool IsActionClaim(string value)
        {
            return value.Equals("a");
        }

        private static bool IsJtiClaim(string value)
        {
            return value.Equals("jti");
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
}
