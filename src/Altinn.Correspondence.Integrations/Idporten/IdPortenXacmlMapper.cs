using Altinn.Authorization.ABAC.Xacml;
using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Common.PEP.Helpers;
using Altinn.Correspondence.Common.Constants;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Services;
using Altinn.Correspondence.Integrations.Altinn.Authorization;
using Microsoft.IdentityModel.Tokens;
using System.Globalization;
using System.Security.Claims;

namespace Altinn.Correspondence.Integrations.Idporten
{
    public static class IdportenXacmlMapper
    {
        internal const string PersonAttributeId = "urn:altinn:person:identifier-no";
        internal const string OrganizationAttributeId = "urn:altinn:organization:identifier-no";
        private const string DefaultType = "string";

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
        public static int? GetMinimumAuthLevel(XacmlJsonResponse response, ClaimsPrincipal user)
        {
            if (!response.Response.Any())
            {
                return null;
            }
            int? minimumAuthLevel = null;
            foreach (var result in response.Response)
            {
                if (!result.Decision.Equals(XacmlContextDecision.Permit.ToString()))
                {
                    return null;
                }

                if (result.Obligations != null)
                {
                    List<XacmlJsonObligationOrAdvice> obligations = result.Obligations;
                    XacmlJsonAttributeAssignment? obligation = GetObligation("urn:altinn:minimum-authenticationlevel", obligations);
                    if (obligation != null)
                    {
                        if (!int.TryParse(obligation.Value, out int currentLevel))
                        {
                            continue;
                        }
                        minimumAuthLevel = minimumAuthLevel.HasValue ? Math.Min(minimumAuthLevel.Value, currentLevel) : currentLevel;
                    }
                }
            }

            return minimumAuthLevel ?? 0;
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

        public static async Task<XacmlJsonRequestRoot> CreateIdPortenDecisionRequest(ClaimsPrincipal user, IAltinnRegisterService altinnRegisterService, List<string> actionTypes, string resourceId, string party, string? instanceId, CancellationToken cancellationToken = default)
        {
            var issuer = user.Claims.FirstOrDefault(c => c.Type == "iss")?.Value ?? string.Empty;
            var subjectCategory = await CreateSubjectCategory(user, altinnRegisterService, cancellationToken);

            XacmlJsonRequest request = new()
            {
                AccessSubject = new List<XacmlJsonCategory> { subjectCategory },
                Action = new List<XacmlJsonCategory>(),
                Resource = new List<XacmlJsonCategory>()
            };
            request.Action.AddRange(actionTypes.Select(action => DecisionHelper.CreateActionCategory(action)));
            request.Resource.Add(XacmlRequestFactory.CreateResourceCategory(resourceId, party, instanceId, issuer));

            return new XacmlJsonRequestRoot { Request = request };
        }

        private static async Task<XacmlJsonCategory> CreateSubjectCategory(ClaimsPrincipal user, IAltinnRegisterService altinnRegisterService, CancellationToken cancellationToken)
        {
            var pidClaim = user.Claims.FirstOrDefault(claim => IsValidPid(claim.Type));
            if (pidClaim is not null)
            {
                return CreateSubjectCategoryFromPid(pidClaim);
            }
            var emailClaim = user.Claims.FirstOrDefault(claim => claim.Type == "email");
            if (emailClaim is null)
            {
                throw new SecurityTokenException("Idporten token does not contain the required pid or email claim");
            }
            return await CreateSubjectCategoryFromEmail(emailClaim, altinnRegisterService, cancellationToken);
        }

        private static XacmlJsonCategory CreateSubjectCategoryFromPid(Claim pidClaim)
        {
            XacmlJsonCategory subjectCategory = new() { Attribute = new List<XacmlJsonAttribute>() };
            subjectCategory.Attribute.Add(
                DecisionHelper.CreateXacmlJsonAttribute(
                    UrnConstants.PersonIdAttribute,
                    pidClaim.Value.WithoutPrefix(),
                    DefaultType,
                    pidClaim.Issuer));
            return subjectCategory;
        }

        private static async Task<XacmlJsonCategory> CreateSubjectCategoryFromEmail(Claim emailClaim, IAltinnRegisterService altinnRegisterService, CancellationToken cancellationToken)
        {
            XacmlJsonCategory subjectCategory = new() { Attribute = new List<XacmlJsonAttribute>() };
            var emailUrn = emailClaim.Value.ToLowerInvariant().WithUrnPrefix();
            var party = await altinnRegisterService.LookUpPartyById(emailUrn, cancellationToken);
            if (party is not null && party.UserId is int userId && userId > 0)
            {
                subjectCategory.Attribute.Add(
                    DecisionHelper.CreateXacmlJsonAttribute(
                        UrnConstants.UserId,
                        userId.ToString(CultureInfo.InvariantCulture),
                        DefaultType,
                        emailClaim.Issuer));
                return subjectCategory;
            }

            return subjectCategory;
        }

        private static bool IsValidPid(string value)
        {
            return value.Equals("pid", StringComparison.Ordinal);
        }

    }
}
