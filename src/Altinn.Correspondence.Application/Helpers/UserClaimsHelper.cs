using System.Security.Claims;
using System.Text.Json;
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
        private const string _scopeClaim = "scope";
        private const string _consumerClaim = "consumer";
        private const string _IdProperty = "ID";
        private const string _dialogportenOrgClaim = "p";

        public UserClaimsHelper(IHttpContextAccessor httpContextAccessor, IOptions<DialogportenSettings> dialogportenSettings)
        {
            _user = httpContextAccessor?.HttpContext?.User ?? new ClaimsPrincipal();
            _claims = _user.Claims ?? [];
            _dialogportenSettings = dialogportenSettings.Value;
        }
        public bool IsAffiliatedWithCorrespondence(string recipientId, string senderId)
        {
            return IsRecipient(recipientId) || IsSender(senderId);
        }
        public bool IsRecipient(string recipientId)
        {
            if (_claims.Any(c => c.Issuer == _dialogportenSettings.Issuer)) return MatchesDialogTokenOrganization(recipientId);
            if (GetUserID() != recipientId) return false;
            if (!GetUserScope().Any(scope => scope.Value == AuthorizationConstants.RecipientScope)) return false;
            return true;
        }
        public bool IsSender(string senderId)
        {
            if (_claims.Any(c => c.Issuer == _dialogportenSettings.Issuer)) return MatchesDialogTokenOrganization(senderId);
            if (GetUserID() != senderId) return false;
            if (!GetUserScope().Any(scope=> scope.Value == AuthorizationConstants.SenderScope)) return false;
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
            return orgValue.Replace("urn:altinn:organization:identifier-no:", "0192:") == organizationId;
        }
        public string? GetUserID()
        {
            var consumer = _claims.FirstOrDefault(c => c.Type == _consumerClaim)?.Value;
            if (consumer is null) return GetDialogportenTokenUserId();

            JsonDocument jsonDoc = JsonDocument.Parse(consumer);
            string? id = jsonDoc.RootElement.GetProperty(_IdProperty).GetString();
            return id;
        }
        private string? GetDialogportenTokenUserId()
        {
            return _claims.FirstOrDefault(c => c.Type == _IdProperty)?.Value;
        }
        private IEnumerable<Claim> GetUserScope()
        {
            var scopes = _claims.Where(c => c.Type == _scopeClaim) ?? [];
            return scopes;
        }
    }
}