using System.Security.Claims;
using System.Text.Json;
using Altinn.Common.PEP.Constants;
using Altinn.Correspondence.Application.Configuration;
using Altinn.Correspondence.Core.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Altinn.Correspondence.Application.Helpers
{
    public class UserClaimsHelper
    {
        private readonly ClaimsPrincipal _user;
        private readonly IEnumerable<Claim> _claims;
        private readonly DialogportenSettings _dialogportenSettings;
        private readonly IdportenSettings _idportenSettings;
        private const string _scopeClaim = "scope";
        private const string _consumerClaim = "consumer";
        private const string _IdProperty = "ID";
        private const string _dialogportenOrgClaim = "p";
        private const string _partyIdClaim = "urn:altinn:partyid";
        private const string _personId = "pid";
        private const string _minAuthLevelClaim = "urn:altinn:authlevel";
        private const string _altinnUrnPersonIdentifier = "urn:altinn:person:identifier-no";

        public UserClaimsHelper(IHttpContextAccessor httpContextAccessor, IOptions<DialogportenSettings> dialogportenSettings, IOptions<IdportenSettings> idportenSettings)
        {
            _user = httpContextAccessor?.HttpContext?.User ?? new ClaimsPrincipal();
            _claims = _user.Claims ?? [];
            _dialogportenSettings = dialogportenSettings.Value;
            _idportenSettings = idportenSettings.Value;
        }
        public int? GetPartyId()
        {
            var partyId = _claims.FirstOrDefault(c => c.Type == _partyIdClaim)?.Value;
            if (partyId is null) return null;
            if (int.TryParse(partyId, out int id)) return id;
            return null;
        }

        public int GetMinimumAuthenticationLevel()
        {
            var authLevelClaim = _claims.FirstOrDefault(c => c.Type == _minAuthLevelClaim);
            if (authLevelClaim is null) return 0;
            if (int.TryParse(authLevelClaim.Value, out int level)) return level;
            return 0;
        }
        public bool IsRecipient(string recipientId)
        {
            if (_claims.Any(c => c.Issuer == _dialogportenSettings.Issuer)) return MatchesDialogTokenOrganization(recipientId) || GetPersonID() == recipientId;
            if (_claims.Any(c => c.Issuer == _idportenSettings.Issuer)) return true; // Idporten tokens are always recipients, verified by altinn authorization
            if (GetUserID() != recipientId && GetPersonID() != recipientId) return false;
            if (!GetUserScope().Any(scope => scope == AuthorizationConstants.RecipientScope)) return false;
            return true;
        }


        public bool IsSender(string senderId)
        {
            if (_claims.Any(c => c.Issuer == _dialogportenSettings.Issuer)) return MatchesDialogTokenOrganization(senderId) || GetPersonID() == senderId;
            if (_claims.Any(c => c.Issuer == _idportenSettings.Issuer)) return false; 
            if (GetUserID() != senderId && GetPersonID() != senderId) return false;
            if (!GetUserScope().Any(scope=> scope == AuthorizationConstants.SenderScope)) return false;
            return true;
        }
        private bool MatchesDialogTokenOrganization(string organizationId)
        {
            var orgClaim = _claims.FirstOrDefault(c => c.Type == _dialogportenOrgClaim);
            if (orgClaim is null)
            {
                return false;
            }
            var orgValue = orgClaim.Value;
            return orgValue.Replace(_altinnUrnPersonIdentifier, "0192:") == organizationId;
        }
        public string? GetUserID()
        {
            var consumer = _claims.FirstOrDefault(c => c.Type == _consumerClaim)?.Value;
            if (consumer is null) return GetDialogportenTokenUserId();

            JsonDocument jsonDoc = JsonDocument.Parse(consumer);
            string? id = jsonDoc.RootElement.GetProperty(_IdProperty).GetString();
            return id;
        }
        private string? GetPersonID()
        {
            if (_claims.Any(c => c.Issuer == _dialogportenSettings.Issuer))
            {
                var personidClaimValue = _claims.FirstOrDefault(c => c.Type == "p")?.Value;
                if (!personidClaimValue.StartsWith(_altinnUrnPersonIdentifier))
                {
                    return null;
                }
                return personidClaimValue.Replace(_altinnUrnPersonIdentifier, "");
            } 
            else if (_claims.Any(c => c.Type == _personId))
            {
                return _claims.FirstOrDefault(c => c.Type == _personId)?.Value;
            } 
            else
            {
                return null;
            }
        }
        private string? GetDialogportenTokenUserId()
        {
            return _claims.FirstOrDefault(c => c.Type == _IdProperty)?.Value;
        }
        private IEnumerable<string> GetUserScope()
        {
            var scopeClaims = _claims.Where(c => c.Type == _scopeClaim) ?? [];
            var scopes = scopeClaims.SelectMany(c => c.Value.Split(" "));
            return scopes;
        }
    }
}