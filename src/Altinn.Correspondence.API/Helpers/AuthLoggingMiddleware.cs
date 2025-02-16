
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System.Text;
using System.IdentityModel.Tokens.Jwt;

namespace Altinn.Correspondence.API.Helpers;

public class AuthLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuthLoggingMiddleware> _logger;

    public AuthLoggingMiddleware(RequestDelegate next, ILogger<AuthLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Log the request path and method
        _logger.LogInformation(
            "Request: {Method} {Path}",
            context.Request.Method,
            context.Request.Path
        );

        // Check for Authorization header
        if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            var token = authHeader.ToString().Replace("Bearer ", "");

            try
            {
                // Decode the JWT token without validation
                var handler = new JwtSecurityTokenHandler();
                if (handler.CanReadToken(token))
                {
                    var jwtToken = handler.ReadJwtToken(token);

                    // Log relevant claims (especially 'aud' claim)
                    var audienceClaims = jwtToken.Claims.Where(c => c.Type == "aud").ToList();

                    _logger.LogInformation("Token Details:");
                    _logger.LogInformation("Issuer: {Issuer}", jwtToken.Issuer);
                    _logger.LogInformation("Audiences: {Audiences}",
                        string.Join(", ", audienceClaims.Select(c => c.Value)));

                    // Log additional relevant claims
                    foreach (var claim in jwtToken.Claims)
                    {
                        _logger.LogInformation("Claim: {Type} = {Value}",
                            claim.Type, claim.Value);
                    }
                }
                else
                {
                    _logger.LogWarning("Invalid JWT token format");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing JWT token");
            }
        }
        else
        {
            _logger.LogWarning("No Authorization header present");
        }

        // Call the next middleware in the pipeline
        await _next(context);

        // Log the response status code
        _logger.LogInformation(
            "Response Status Code: {StatusCode}",
            context.Response.StatusCode
        );
    }
}

// Extension method to make it easier to add the middleware
public static class AuthLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseAuthLogging(
        this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<AuthLoggingMiddleware>();
    }
}