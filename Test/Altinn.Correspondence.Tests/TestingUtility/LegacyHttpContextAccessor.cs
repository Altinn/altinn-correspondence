using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace Altinn.Correspondence.Tests.TestingUtility;

public class LegacyHttpContextAccessor : IHttpContextAccessor
{
    HttpContext? IHttpContextAccessor.HttpContext
    {
        get
        {
            var httpContext = new DefaultHttpContext()
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim("urn:altinn:partyid", "123456789")
                ]))
            };
            return httpContext;
        }
        set => throw new NotImplementedException();
    }
}