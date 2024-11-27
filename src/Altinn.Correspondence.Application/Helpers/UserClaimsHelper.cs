using Altinn.Common.PEP.Constants;
using Altinn.Correspondence.Common.Helpers;
using Altinn.Correspondence.Core.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace Altinn.Correspondence.Application.Helpers
{
    public class UserClaimsHelper
    {
        private readonly ClaimsPrincipal _user;
        private readonly IEnumerable<Claim> _claims;
        private const string _minAuthLevelClaim = "urn:altinn:authlevel";

        public UserClaimsHelper(IHttpContextAccessor httpContextAccessor)
        {
            _user = httpContextAccessor?.HttpContext?.User ?? new ClaimsPrincipal();
            _claims = _user.Claims ?? [];
        }
        public int? GetPartyId()
        {
            var partyId = _claims.FirstOrDefault(c => c.Type == AltinnXacmlUrns.PartyId)?.Value;
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
    }
}