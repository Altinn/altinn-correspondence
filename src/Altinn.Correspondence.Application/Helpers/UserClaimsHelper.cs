using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Altinn.Correspondence.Application.Helpers
{
    public class UserClaimsHelper
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public UserClaimsHelper(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }
        public string? GetUserID()
        {
            var user = _httpContextAccessor?.HttpContext?.User;
            if (user is null) return null;

            var claims = user.Claims ?? [];
            var consumer = claims.FirstOrDefault(c => c.Type == "consumer")?.Value;
            if (consumer is null) return null;

            JsonDocument jsonDoc = JsonDocument.Parse(consumer);
            string? id = jsonDoc.RootElement.GetProperty("ID").GetString();
            return id;
        }
    }
}