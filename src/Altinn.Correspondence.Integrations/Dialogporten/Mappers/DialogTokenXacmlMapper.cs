﻿using Altinn.Authorization.ABAC.Xacml;
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
            var actions = actionClaim.Value.Split(';');
            XacmlJsonCategory actionAttributes = new()
            {
                Attribute = new List<XacmlJsonAttribute>()
            };
            foreach (var action in actions)
            {
                actionAttributes.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(MatchAttributeIdentifiers.ActionId, action, DefaultType, actionClaim.Issuer, includeResult));
            }
            return actionAttributes;
        }

        private static XacmlJsonCategory CreateResourceCategory(string resourceId, ClaimsPrincipal user)
        {
            XacmlJsonCategory resourceCategory = new() { Attribute = new List<XacmlJsonAttribute>() };
            resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.ResourceId, resourceId, DefaultType, DefaultIssuer));
            var orgClaim = user.Claims.FirstOrDefault(claim => IsOrgClaim(claim.Type));
            if (orgClaim is not null)
            {
                resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute("urn:altinn:organization:identifier-no", orgClaim.Value.Replace("urn:altinn:organization:identifier-no:", ""), DefaultType, DefaultIssuer));
            }
            return resourceCategory;
        }

        private static XacmlJsonCategory CreateSubjectCategory(ClaimsPrincipal user)
        {
            XacmlJsonCategory xacmlJsonCategory = new XacmlJsonCategory();
            List<XacmlJsonAttribute> list = new List<XacmlJsonAttribute>();

            foreach (Claim claim in user.Claims)
            {
                if (IsJtiClaim(claim.Type))
                {
                    list.Add(CreateXacmlJsonAttribute("urn:altinn:sessionid", claim.Value, "string", claim.Issuer));
                }
                else if (IsValidUrn(claim.Type))
                {
                    list.Add(CreateXacmlJsonAttribute(claim.Type, claim.Value, "string", claim.Issuer));
                }
                else if (IsSsnClaim(claim.Type))
                {
                    list.Add(CreateXacmlJsonAttribute("urn:altinn:person:identifier-no", claim.Value.Replace("urn:altinn:person:identifier-no:", ""), "string", claim.Issuer));
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

        public static bool ValidateDialogportenResult(XacmlJsonResponse response, ClaimsPrincipal user)
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
                    XacmlJsonAttributeAssignment obligation = GetObligation("urn:altinn:minimum-authenticationlevel", obligations);
                    if (obligation != null)
                    {
                        string value = obligation.Value;
                        string value2 = user.Claims.FirstOrDefault((Claim c) => c.Type.Equals("l")).Value;
                        if (Convert.ToInt32(value2) < Convert.ToInt32(value))
                        {
                            return false;
                        }
                    }
                }
                return true;
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
