﻿using Altinn.Authorization.ABAC.Xacml;
using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Common.PEP.Helpers;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Common.Helpers;
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

        public static XacmlJsonRequestRoot CreateDialogportenDecisionRequest(ClaimsPrincipal user, string resourceId, string party, string? instanceId)
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
            var resourceCategory = CreateResourceCategory(resourceId, party, instanceId);
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

        private static XacmlJsonCategory CreateResourceCategory(string resourceId, string party, string? instanceId)
        {
            XacmlJsonCategory resourceCategory = new() { Attribute = new List<XacmlJsonAttribute>() };
            resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(UrnConstants.Resource, resourceId, DefaultType, DefaultIssuer));
            var partyWithoutPrefix = party.WithoutPrefix();
            if (partyWithoutPrefix.IsOrganizationNumber())
            {
                resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(UrnConstants.OrganizationNumberAttribute, partyWithoutPrefix, DefaultType, DefaultIssuer));
            }
            else if (partyWithoutPrefix.IsSocialSecurityNumber())
            {
                resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(UrnConstants.PersonIdAttribute, partyWithoutPrefix, DefaultType, DefaultIssuer));
            }
            else
            {
                throw new InvalidOperationException("RecipientId is not a valid organization or person number");
            }
            if (instanceId is not null)
            {
                resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(UrnConstants.ResourceInstance, instanceId, DefaultType, DefaultIssuer));
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
                    list.Add(CreateXacmlJsonAttribute(UrnConstants.SessionId, claim.Value, DefaultType, claim.Issuer));
                }
                else if (IsValidUrn(claim.Type))
                {
                    list.Add(CreateXacmlJsonAttribute(claim.Type, claim.Value, DefaultType, claim.Issuer));
                }
                else if (IsConsumerClaim(claim.Type))
                {
                    list.Add(CreateXacmlJsonAttribute(UrnConstants.PersonIdAttribute, claim.Value.WithoutPrefix(), DefaultType, claim.Issuer));
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

        private static bool IsConsumerClaim(string value)
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
                    XacmlJsonAttributeAssignment obligation = GetObligation(UrnConstants.MinimumAuthenticationLevel, obligations);
                    if (obligation != null)
                    {
                        string obligationRequiredLevel = obligation.Value;
                        string claimLevel = user.Claims.FirstOrDefault((Claim c) => c.Type.Equals("l")).Value;
                        if (claimLevel == "0")
                        {
                            return true; // Hotfix until Dialogporten starts sending correct level
                        }
                        if (Convert.ToInt32(claimLevel) < Convert.ToInt32(obligationRequiredLevel))
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
