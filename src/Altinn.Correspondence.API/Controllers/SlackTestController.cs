using Microsoft.AspNetCore.Mvc;
using Slack.Webhooks;
using Altinn.Correspondence.Core.Options;

namespace Altinn.Correspondence.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SlackTestController : ControllerBase
{
    private readonly ISlackClient _slackClient;
    private readonly SlackSettings _slackSettings;
    private readonly ILogger<SlackTestController> _logger;

    public SlackTestController(
        ISlackClient slackClient, 
        SlackSettings slackSettings,
        ILogger<SlackTestController> logger)
    {
        _slackClient = slackClient;
        _slackSettings = slackSettings;
        _logger = logger;
    }

    /// <summary>
    /// Get current Slack configuration status
    /// </summary>
    /// <returns>Current Slack configuration information</returns>
    [HttpGet("config")]
    [Authorize] // Add appropriate authorization
    public IActionResult GetSlackConfig()
    {
        var hostEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

        // Only allow in non-production environments
        if (!hostEnvironment.Equals("Development", StringComparison.OrdinalIgnoreCase) &&
            !hostEnvironment.Equals("Staging", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }
        
        return Ok(new
        {
            channel = _slackSettings.NotificationChannel,
            environment = hostEnvironment,
            slackClientType = _slackClient.GetType().Name,
            isDevelopment = hostEnvironment.Equals("Development", StringComparison.OrdinalIgnoreCase)
        });
    }

    /// <summary>
    /// Send a simple test message to Slack
    /// </summary>
    /// <param name="message">The message to send</param>
    /// <returns>Result indicating success or failure</returns>
    [HttpPost("send-simple-message")]
    public async Task<IActionResult> SendSimpleMessage([FromBody] string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return BadRequest(new { success = false, message = "Message cannot be empty" });
        }

        try
        {
            var slackMessage = new SlackMessage
            {
                Text = $"Test Message: {message}",
                Channel = _slackSettings.NotificationChannel
            };

            var success = await _slackClient.PostAsync(slackMessage);

            if (success)
            {
                _logger.LogInformation("Simple test message sent successfully");
                return Ok(new { 
                    success = true, 
                    message = "Simple test message sent successfully",
                    channel = _slackSettings.NotificationChannel
                });
            }
            else
            {
                _logger.LogWarning("Failed to send simple test message");
                return BadRequest(new { 
                    success = false, 
                    message = "Failed to send simple test message",
                    channel = _slackSettings.NotificationChannel
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending simple test message");
            return StatusCode(500, new { 
                success = false, 
                message = "Internal server error while sending simple test message"
            });
        }
    }
} 