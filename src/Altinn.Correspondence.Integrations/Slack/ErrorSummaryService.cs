using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Slack.Webhooks;

namespace Altinn.Correspondence.Integrations.Slack;

public class ErrorSummaryService : BackgroundService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<ErrorSummaryService> _logger;
    private readonly ISlackClient _slackClient;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ErrorAggregationService _errorAggregationService;
    private readonly TimeSpan _interval = TimeSpan.FromMinutes(3);

    public ErrorSummaryService(
        IDistributedCache cache,
        ILogger<ErrorSummaryService> logger,
        ISlackClient slackClient,
        IHostEnvironment hostEnvironment,
        ErrorAggregationService errorAggregationService)
    {
        _cache = cache;
        _logger = logger;
        _slackClient = slackClient;
        _hostEnvironment = hostEnvironment;
        _errorAggregationService = errorAggregationService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SendSummaries();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while sending summaries");
            }

            await Task.Delay(_interval, stoppingToken);
        }
    }

    private async Task SendSummaries()
    {
        var activeErrors = await _errorAggregationService.GetActiveErrors();
        if (!activeErrors.Any())
        {
            return;
        }

        var environment = _hostEnvironment.IsDevelopment() ? "Development" : "Production";
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");
        var channel = _hostEnvironment.IsDevelopment() ? "#test-varslinger" : "#mf-varsling-critical";

        foreach (var (message, count) in activeErrors)
        {
            var slackMessage = new SlackMessage
            {
                Channel = channel,
                Text = $"🚨 *Error in {environment}*\n" +
                       $"*Time:* {timestamp}\n" +
                       $"*Error Count:* {count}\n" +
                       $"*Message:* {message}"
            };

            try
            {
                await _slackClient.PostAsync(slackMessage);
                _logger.LogInformation("Sent error notification for {Message} with count {Count}", message, count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send error notification");
            }
        }
        
        // Clear the active errors after sending all notifications
        await _errorAggregationService.ClearActiveErrors();
    }
} 