using Altinn.Correspondence.Common.Constants;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.Helpers
{
    public class UserClaimsHelper
    {
        private readonly ClaimsPrincipal _user;
        private readonly IEnumerable<Claim> _claims;

        public UserClaimsHelper(IHttpContextAccessor httpContextAccessor)
        {
            _user = httpContextAccessor?.HttpContext?.User ?? new ClaimsPrincipal();
            _claims = _user.Claims ?? [];
        }
        public int? GetPartyId()
        {
            var partyId = _claims.FirstOrDefault(c => c.Type == UrnConstants.Party)?.Value;
            if (partyId is null) return null;
            if (int.TryParse(partyId, out int id)) return id;
            return null;
        }

        public string? GetUserId() => _claims.FirstOrDefault(c => c.Type == UrnConstants.UserId)?.Value;

        public int GetMinimumAuthenticationLevel()
        {
            var authLevelClaim = _claims.FirstOrDefault(c => c.Type == UrnConstants.AuthenticationLevel);
            if (authLevelClaim is null) return 0;
            if (int.TryParse(authLevelClaim.Value, out int level)) return level;
            return 0;
        }
    }
}