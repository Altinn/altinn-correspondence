using Altinn.Authorization.ABAC.Xacml.JsonProfile;
using Altinn.Common.PEP.Constants;
using Altinn.Common.PEP.Helpers;
using static Altinn.Authorization.ABAC.Constants.XacmlConstants;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.IdentityModel.Tokens;

namespace Altinn.Correspondence.Integrations.Dialogporten.Mappers
{
    public static class DialogportenXacmlMapper
    {    /// <summary>
         /// Default issuer for attributes
         /// </summary>
        internal const string DefaultIssuer = "Dialogporten";

        /// <summary>
        /// Default type for attributes
        /// </summary>
        internal const string DefaultType = "string";

        /// <summary>
        /// Subject id for multi requests. Inde should be appended.
        /// </summary>
        internal const string SubjectId = "s";

        /// <summary>
        /// Action id for multi requests. Inde should be appended.
        /// </summary>
        internal const string ActionId = "a";

        /// <summary>
        /// Resource id for multi requests. Inde should be appended.
        /// </summary>
        internal const string ResourceId = "r";

        /// <param name="actionType">Action type represented as a string</param>
        /// <param name="includeResult">A value indicating whether the value should be included in the result</param>
        /// <returns>A XacmlJsonCategory</returns>
        internal static XacmlJsonCategory CreateActionCategory(ClaimsPrincipal user, bool includeResult = false)
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

        /// If id is required this should be included by the caller. 
        /// Attribute eventId is tagged with `includeInResponse`</remarks>
        internal static XacmlJsonCategory CreateResourceCategory(string resourceId, ClaimsPrincipal user)
        {
            XacmlJsonCategory resourceCategory = new() { Attribute = new List<XacmlJsonAttribute>() };
            resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute(AltinnXacmlUrns.ResourceId, resourceId, DefaultType, DefaultIssuer));
            var claim = user.Claims.FirstOrDefault(claim => IsOrgClaim(claim.Type));
            if (claim is not null)
            {
                resourceCategory.Attribute.Add(DecisionHelper.CreateXacmlJsonAttribute("urn:altinn:organization:identifier-no", claim.Value, DefaultType, DefaultIssuer));
            }
            return resourceCategory;
        }

        internal static XacmlJsonCategory CreateSubjectCategory(ClaimsPrincipal user)
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
