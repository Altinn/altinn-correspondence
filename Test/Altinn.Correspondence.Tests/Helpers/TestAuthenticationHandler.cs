using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Altinn.Correspondence.Tests.Helpers;

public class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
	private readonly List<Claim> _claims;

	public TestAuthenticationHandler(
		IOptionsMonitor<AuthenticationSchemeOptions> options,
		ILoggerFactory logger,
		UrlEncoder encoder,
		ISystemClock clock,
		IHttpContextAccessor httpContextAccessor)
		: base(options, logger, encoder, clock)
	{
		var claimsJson = httpContextAccessor.HttpContext?.Request.Headers["X-Custom-Claims"].ToString();
		if (!string.IsNullOrEmpty(claimsJson))
		{
			var claimsData = JsonSerializer.Deserialize<List<Dictionary<string, string>>>(claimsJson);
			_claims = claimsData.Select(c => new Claim(c["Type"], c["Value"])).ToList();
		}
	}

	protected override Task<AuthenticateResult> HandleAuthenticateAsync()
	{
		var identity = new ClaimsIdentity(_claims, "Test");
		var principal = new ClaimsPrincipal(identity);
		var ticket = new AuthenticationTicket(principal, "Test");
		return Task.FromResult(AuthenticateResult.Success(ticket));
	}
}
