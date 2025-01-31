using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

public class SlackNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly string _slackWebhookUrl;
    private readonly ILogger<SlackNotificationService> _logger;

    public SlackNotificationService(IConfiguration configuration, ILogger<SlackNotificationService> logger)
    {
        _httpClient = new HttpClient();
        _slackWebhookUrl = configuration["Slack:WebhookUrl"]; // Read from appsettings.json
        _logger = logger;
    }

    public async Task SendSlackMessageAsync(string message)
    {
        if (string.IsNullOrEmpty(_slackWebhookUrl))
        {
            _logger.LogError("Slack Webhook URL is missing.");
            return;
        }

        var payload = new { text = message };
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(_slackWebhookUrl, content);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Slack API error: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Failed to send Slack message: {ex.Message}");
        }
    }
}
