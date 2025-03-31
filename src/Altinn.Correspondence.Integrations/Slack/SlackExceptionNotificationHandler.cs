using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Diagnostics;
using Slack.Webhooks;
using Microsoft.AspNetCore.Http;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using Altinn.Correspondence.Core.Options;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Altinn.Correspondence.Integrations.Slack;

public class SlackExceptionNotificationHandler : IExceptionHandler
{
    private readonly ILogger<SlackExceptionNotificationHandler> _logger;
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ISlackClient _slackClient;
    private readonly SlackSettings _slackSettings;
    private readonly ErrorAggregationService _errorAggregationService;

    public SlackExceptionNotificationHandler(
        ILogger<SlackExceptionNotificationHandler> logger,
        IProblemDetailsService problemDetailsService,
        IHostEnvironment hostEnvironment,
        ISlackClient slackClient,
        IOptions<SlackSettings> slackSettings,
        ErrorAggregationService errorAggregationService)
    {
        _logger = logger;
        _problemDetailsService = problemDetailsService;
        _hostEnvironment = hostEnvironment;
        _slackClient = slackClient;
        _slackSettings = slackSettings.Value;
        _errorAggregationService = errorAggregationService;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var message = exception.Message;
        var source = GetErrorSource(httpContext);
        var (shouldSend, count) = await _errorAggregationService.ShouldSendNotification(message, exception, source);

        if (shouldSend)
        {
            await SendSlackNotificationWithMessage(message, exception, count, source);
        }

        return false;
    }

    public async ValueTask<bool> TryHandleAsync(
        string jobId,
        string jobName,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var message = $"Job {jobName} ({jobId}) failed: {exception.Message}";
        var source = $"Job: {jobName}";
        var (shouldSend, count) = await _errorAggregationService.ShouldSendNotification(message, exception, source);
        
        if (shouldSend)
        {
            await SendSlackNotificationWithMessage(message, exception, count, source);
        }

        return false;
    }

    private async Task SendSlackNotificationWithMessage(string message, Exception exception, int count, string source)
    {
        var environment = _hostEnvironment.IsDevelopment() ? "Development" : "Production";
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss UTC");
        var stackTrace = exception.StackTrace ?? "No stack trace available";
        var channel = _hostEnvironment.IsDevelopment() ? "#test-varslinger" : "#mf-varsling-critical";
        var errorType = exception.GetType().Name;

        var slackMessage = new SlackMessage
        {
            Channel = channel,
            Text = $"🚨 *Error in {environment}*\n" +
                   $"*Time:* {timestamp}\n" +
                   $"*Error Type:* {errorType}\n" +
                   $"*Error Count:* {count}\n" +
                   $"*Source:* {source}\n" +
                   $"*Message:* {message}\n" +
                   $"*Stack Trace:*\n```{stackTrace}```"
        };

        try
        {
            await _slackClient.PostAsync(slackMessage);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Slack notification");
        }
    }

    private string GetErrorSource(HttpContext httpContext)
    {
        var path = httpContext.Request.Path.Value ?? "Unknown";
        var method = httpContext.Request.Method;
        return $"{method} {path}";
    }
}