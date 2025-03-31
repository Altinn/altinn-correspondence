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
    private readonly SlackNotificationService _slackNotificationService;
    private readonly ErrorAggregationService _errorAggregationService;

    public SlackExceptionNotificationHandler(
        ILogger<SlackExceptionNotificationHandler> logger,
        IProblemDetailsService problemDetailsService,
        IHostEnvironment hostEnvironment,
        SlackNotificationService slackNotificationService,
        ErrorAggregationService errorAggregationService)
    {
        _logger = logger;
        _problemDetailsService = problemDetailsService;
        _hostEnvironment = hostEnvironment;
        _slackNotificationService = slackNotificationService;
        _errorAggregationService = errorAggregationService;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var message = exception.Message;
        var shouldSend = await _errorAggregationService.ShouldSendNotification(message);
        
        if (shouldSend)
        {
            await SendSlackNotificationWithMessage(message, exception);
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
        var shouldSend = await _errorAggregationService.ShouldSendNotification(message);
        
        if (shouldSend)
        {
            await SendSlackNotificationWithMessage(message, exception);
        }

        return false;
    }

    private async Task SendSlackNotificationWithMessage(string message, Exception exception)
    {
        var stackTrace = exception.StackTrace;
        var environment = _hostEnvironment.EnvironmentName;
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");

        var slackMessage = $@"🚨 *Exception Alert*
*Environment:* {environment}
*Time:* {timestamp}
*Message:* {message}
*Stack Trace:* {stackTrace}";

        await _slackNotificationService.SendSlackMessageAsync(slackMessage);
    }
}