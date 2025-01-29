using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Diagnostics;
using Slack.Webhooks;
using Microsoft.AspNetCore.Diagnostics;  
using Slack.Webhooks;  
using Microsoft.AspNetCore.Http;  

namespace Altinn.Correspondence.Integrations.Slack;
public class SlackExceptionNotification : IExceptionHandler
{
    private readonly ILogger<SlackExceptionNotification> _logger;
    private readonly ISlackClient _slackClient;
    private const string TestChannel = "#test-varslinger";
    private readonly IHostEnvironment _hostEnvironment;

    public SlackExceptionNotification(ILogger<SlackExceptionNotification> logger, ISlackClient slackClient, IHostEnvironment hostEnvironment)
    {
        _logger = logger;
        _slackClient = slackClient;
        _hostEnvironment = hostEnvironment;
    }
    public ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var exceptionMessage = FormatExceptionMessage(exception, httpContext);

        _logger.LogError(
            exception,
            "Unhandled exception occurred. Type: {ExceptionType}, Message: {Message}, Path: {Path}",
            exception.GetType().Name,
            exception.Message,
            httpContext.Request.Path);
        
        try
        {
            SendSlackNotificationWithMessage(exceptionMessage);
        }
        catch (Exception slackEx)
        {
            _logger.LogError(
                slackEx,
                "Failed to send Slack notification");
        }

        return ValueTask.FromResult(false);
    }

    public async ValueTask<bool> TryHandleAsync(string jobId, string jobName, Exception exception, CancellationToken cancellationToken)
    {
        var exceptionMessage = FormatExceptionMessage(jobId, jobName, exception);

        _logger.LogError(
            exception,
            "Unhandled exception occurred. Job ID: {JobId}, Job Name: {JobName}, Type: {ExceptionType}, Message: {Message}",
            jobId,
            jobName,
            exception.GetType().Name,
            exception.Message);

        // Send the exception details to Slack
        var slackMessage = new SlackMessage
        {
            Text = exceptionMessage,
            Channel = TestChannel // Replace with your actual Slack channel
        };

        try
        {
            SendSlackNotificationWithMessage(exceptionMessage);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send Slack notification");
            return false;
        }
    }   

    private string FormatExceptionMessage(Exception exception, HttpContext context)
    {
        return $":warning: *Unhandled Exception*\n" +
               $"*Environment:* {_hostEnvironment.EnvironmentName}\n" +
               $"*System:* Correspondence\n" +
               $"*Type:* {exception.GetType().Name}\n" +
               $"*Message:* {exception.Message}\n" +
               $"*Path:* {context.Request.Path}\n" +
               $"*Time:* {DateTime.UtcNow:u}\n" +
               $"*Stacktrace:* \n{exception.StackTrace}";
    }

    private string FormatExceptionMessage(string jobId, string jobName, Exception exception)
    {
        return $":warning: *Unhandled Exception*\n" +
                $"*Job ID:* {jobId}\n" +
                $"*Job Name:* {jobName}\n" +
                $"*Type:* {exception.GetType().Name}\n" +
                $"*Message:* {exception.Message}\n" +
                $"*Stacktrace:* \n{exception.StackTrace}";
    }

    private void SendSlackNotificationWithMessage(string message)
    {
        var slackMessage = new SlackMessage
        {
            Text = message,
            Channel = TestChannel,
        };
        _slackClient.Post(slackMessage);
    }
}