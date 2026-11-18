using Altinn.Authorization.ABAC.Xacml;
using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Common.PEP.Constants;
using Altinn.Common.PEP.Helpers;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Extensions;
using Altinn.Correspondence.Core.Services;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Altinn.Correspondence.Integrations.Altinn.Authorization;
using static Altinn.Authorization.ABAC.Constants.XacmlConstants;

namespace Altinn.Correspondence.Integrations.Dialogporten.Mappers
{
    public static class DialogTokenXacmlMapper
    {
        internal const string DefaultIssuer = "Dialogporten";
        internal const string DefaultType = "string";
        private const int SystemUserAuthenticationLevel = 3;

        public static async Task<XacmlJsonRequestRoot> CreateDialogportenDecisionRequest(ClaimsPrincipal user, IAltinnRegisterService altinnRegisterService, string resourceId, string party, string? instanceId, CancellationToken cancellationToken = default)
        {
            XacmlJsonRequest request = new()
            {
                AccessSubject = new List<XacmlJsonCategory>(),
                Action = new List<XacmlJsonCategory>(),
                Resource = new List<XacmlJsonCategory>()
            };

            var subjectCategory = await CreateSubjectCategory(user, altinnRegisterService, cancellationToken);
            request.AccessSubject.Add(subjectCategory);
            request.Action.Add(CreateActionCategory(user));
            var resourceCategory = XacmlRequestFactory.CreateResourceCategory(resourceId, party, instanceId, DefaultIssuer);
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

        private static async Task<XacmlJsonCategory> CreateSubjectCategory(ClaimsPrincipal user, IAltinnRegisterService altinnRegisterService, CancellationToken cancellationToken)
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
                    var identifier = claim.Value;

                    if (IsSystemUserIdentifier(identifier))
                    {
                        list = new List<XacmlJsonAttribute>();
                        list.Add(CreateXacmlJsonAttribute(AltinnXacmlUrns.SystemUserUuid, identifier.WithoutPrefix(), DefaultType, claim.Issuer));
                        break;
                    }
                    if (identifier.IsSocialSecurityNumber())
                    {
                        list.Add(CreateXacmlJsonAttribute(UrnConstants.PersonIdAttribute, identifier.WithoutPrefix(), DefaultType, claim.Issuer));
                    }
                    else if (identifier.IsIdPortenEmailUrn() || identifier.IsLegacySelfIdentifiedUrn())
                    {
                        var party = await altinnRegisterService.LookUpPartyById(identifier, cancellationToken);
                        if (party?.GetUserId() is int userId && userId > 0)
                        {
                            list.Add(CreateXacmlJsonAttribute(UrnConstants.UserId, userId.ToString(), DefaultType, claim.Issuer));
                        }
                    }
                }
                else if (IsSystemUserClaim(claim.Type))
                {
                    list = new List<XacmlJsonAttribute>();
                    list.Add(CreateXacmlJsonAttribute(AltinnXacmlUrns.SystemUserUuid, claim.Value.WithoutPrefix(), DefaultType, claim.Issuer));
                    break;
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

        private static bool IsSystemUserClaim(string value)
        {
            return value.Equals("y");
        }

        private static bool IsSystemUserIdentifier(string value)
        {
            return value.StartsWith($"{UrnConstants.SystemUser}:", StringComparison.Ordinal);
        }

        private static bool IsSystemUser(ClaimsPrincipal user)
        {
            return user.Claims.Any(claim =>
                (IsSystemUserClaim(claim.Type) && IsSystemUserIdentifier(claim.Value))
                || (IsConsumerClaim(claim.Type) && IsSystemUserIdentifier(claim.Value)));
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
                    XacmlJsonAttributeAssignment? obligation = GetObligation(UrnConstants.MinimumAuthenticationLevel, obligations);
                    if (obligation != null)
                    {
                        string obligationRequiredLevel = obligation.Value;
                        string? claimLevel = user.Claims.FirstOrDefault((Claim c) => c.Type.Equals("l"))?.Value;
                        if (Convert.ToInt32(claimLevel) < Convert.ToInt32(obligationRequiredLevel)
                            && !SystemUserMeetsAuthenticationLevelRequirement(user, obligations, obligationRequiredLevel))
                        {
                            return false;
                        }
                    }
                }
                return true;
            }

            return true;
        }

        /// <summary>
        /// The system user obligation only relaxes the general requirement, and only for a system user.
        /// A system user dialog token carries the system user in <c>c</c> as <c>urn:altinn:systemuser:uuid:...</c> and is treated as level 3.
        /// </summary>
        private static bool SystemUserMeetsAuthenticationLevelRequirement(ClaimsPrincipal user, List<XacmlJsonObligationOrAdvice> obligations, string requiredLevel)
        {
            if (!IsSystemUser(user))
            {
                return false;
            }

            var systemUserObligation = GetObligation(UrnConstants.MinimumAuthenticationLevelSystemUser, obligations);
            if (systemUserObligation == null)
            {
                return false;
            }

            return SystemUserAuthenticationLevel >= Convert.ToInt32(systemUserObligation.Value)
                || SystemUserAuthenticationLevel >= Convert.ToInt32(requiredLevel);
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
