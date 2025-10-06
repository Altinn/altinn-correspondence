using Altinn.Correspondence.Core.Options;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Slack.Webhooks;
using System.Diagnostics;
using System.Net;

namespace Altinn.Correspondence.Integrations.Slack;
public class SlackExceptionNotificationHandler(
    ILogger<SlackExceptionNotificationHandler> logger,
    ISlackClient slackClient,
    IProblemDetailsService problemDetailsService,
    IHostEnvironment hostEnvironment,
    SlackSettings slackSettings) : IExceptionHandler
{
    private string Channel => slackSettings.NotificationChannel;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var requestPath = httpContext.Request.Path.ToString();
        if (requestPath.StartsWith("/correspondence/api/v1/migration", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogError(exception, "Unhandled exception on migration endpoint {Path}. Skipping Slack notification.", requestPath);
            return true;
        }
        var exceptionMessage = FormatExceptionMessage(exception, httpContext);
        var sanitizedPath = httpContext.Request.Path.ToString().Replace("\n", "").Replace("\r", "");
        logger.LogError(
            exception,
            "Unhandled exception occurred. Type: {ExceptionType}, Message: {Message}, Path: {Path}, User: {User}, TraceId: {TraceId}, Environment: {Environment}, SlackMessage: {SlackMessage}",
            exception.GetType().Name,
            exception.Message,
            sanitizedPath,
            httpContext.User?.Identity?.Name ?? "Unknown",
            Activity.Current?.Id ?? httpContext.TraceIdentifier,
            hostEnvironment.EnvironmentName,
            exceptionMessage // SlackMessage
        );
        try
        {
            await SendSlackNotificationWithMessage(exceptionMessage);
            var statusCode = HttpStatusCode.InternalServerError;
            var problemDetails = new ProblemDetails
            {
                Status = (int) statusCode,
                Title = "Internal Server Error",
                Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1",
                Detail = hostEnvironment.IsDevelopment() ? exception.Message : "",
                Instance = httpContext.Request.Path
            };
            var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;
            problemDetails.Extensions["traceId"] = traceId;
            await problemDetailsService.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails = problemDetails
            });
            return true;
        }
        catch (Exception slackEx)
        {
            logger.LogError(
                slackEx,
                "Failed to send Slack notification");
            return true;
        }
    }

    public async ValueTask<bool> TryHandleAsync(string jobId, string jobName, Exception exception, int retryCount, CancellationToken cancellationToken)
    {
        var shouldNotify = retryCount == 3 || retryCount == 6 || retryCount == 10;
        if (!shouldNotify)
        {
            logger.LogInformation("Skipping Slack notification for job {JobId} on retry {RetryCount} (only notifying on retries 3, 6, and 10)", jobId, retryCount);
            return true;
        }
        var exceptionMessage = FormatExceptionMessage(jobId, jobName, exception, retryCount);
        logger.LogError(
            exception,
            "Unhandled exception occurred. Job ID: {JobId}, Job Name: {JobName}, RetryCount: {RetryCount}, Type: {ExceptionType}, Message: {Message}, Environment: {Environment}, SlackMessage: {SlackMessage}",
            jobId,
            jobName,
            retryCount,
            exception.GetType().Name,
            exception.Message,
            hostEnvironment.EnvironmentName,
            exceptionMessage // SlackMessage
        );
        try
        {
            await SendSlackNotificationWithMessage(exceptionMessage);
            return true;
        }
        catch (Exception slackEx)
        {
            logger.LogError(slackEx, "Failed to send Slack notification");
            return false;
        }
    }

    private string FormatExceptionMessage(Exception exception, HttpContext context)
    {
        var sanitizedPath = context.Request.Path.ToString().Replace("\n", "").Replace("\r", "");
        return $":warning: *Unhandled Exception*\n" +
               $"*Environment:* {hostEnvironment.EnvironmentName}\n" +
               $"*System:* Correspondence\n" +
               $"*Type:* {exception.GetType().Name}\n" +
               $"*Message:* {exception.Message}\n" +
               $"*Path:* {sanitizedPath}\n" +
               $"*Time:* {DateTime.UtcNow:u}\n" +
               $"*Stacktrace:* \n{exception.StackTrace}";
    }

    private string FormatExceptionMessage(string jobId, string jobName, Exception exception, int retryCount)
    {
        // Use error severity for final retry, warning for intermediate retries
        var severity = retryCount == 10 ? ":x:" : ":warning:";
        var severityText = retryCount == 10 ? "CRITICAL FAILURE" : "Unhandled Exception";
        
        return $"{severity} *{severityText}*\n" +
                $"*Environment:* {hostEnvironment.EnvironmentName}\n" +
                $"*System:* Correspondence\n" +
                $"*Job ID:* {jobId}\n" +
                $"*Job Name:* {jobName}\n" +
                $"*Retry Count:* {retryCount}\n" +
                $"*Type:* {exception.GetType().Name}\n" +
                $"*Message:* {exception.Message}\n" +
                $"*Stacktrace:* \n{exception.StackTrace}\n" + 
                $"*InnerStacktrace:* \n{exception.InnerException?.StackTrace}";
    }
    

    private async Task SendSlackNotificationWithMessage(string message)
    {
        var slackMessage = new SlackMessage
        {
            Text = message,
            Channel = Channel,
        };
        await slackClient.PostAsync(slackMessage);
    }
}