using System.Security.Claims;
using System.Text.Json;
using Altinn.Correspondence.Application.Configuration;
using Microsoft.AspNetCore.Http;

namespace Altinn.Correspondence.Application.Helpers
{
    public class UserClaimsHelper
    {
        private readonly ClaimsPrincipal _user;
        private readonly IEnumerable<Claim> _claims;
        private const string _scopeClaim = "scope";
        private const string _consumerClaim = "consumer";
        private const string _IdProperty = "ID";

        public UserClaimsHelper(IHttpContextAccessor httpContextAccessor)
        {
            _user = httpContextAccessor?.HttpContext?.User ?? new ClaimsPrincipal();
            _claims = _user.Claims ?? [];
        }
        public bool IsAffiliatedWithCorrespondence(string recipientId, string senderId)
        {
            return IsRecipient(recipientId) || IsSender(senderId);
        }
        public bool IsRecipient(string recipientId)
        {
            if (GetUserID() != recipientId) return false;
            if (!GetUserScope().Any(scope => scope.Value == AuthorizationConstants.RecipientScope)) return false;
            return true;
        }
        public bool IsSender(string senderId)
        {
            if (GetUserID() != senderId) return false;
            if (!GetUserScope().Any(scope=> scope.Value == AuthorizationConstants.SenderScope)) return false;
            return true;
        }
        public string? GetUserID()
        {
            var consumer = _claims.FirstOrDefault(c => c.Type == _consumerClaim)?.Value;
            if (consumer is null) return null;

            JsonDocument jsonDoc = JsonDocument.Parse(consumer);
            string? id = jsonDoc.RootElement.GetProperty(_IdProperty).GetString();
            return id;
        }
        private IEnumerable<Claim> GetUserScope()
        {
            var scopes = _claims.Where(c => c.Type == _scopeClaim) ?? [];
            return scopes;
        }
    }
}