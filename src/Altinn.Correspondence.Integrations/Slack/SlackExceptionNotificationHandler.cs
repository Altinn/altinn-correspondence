using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Diagnostics;
using Slack.Webhooks;
using Microsoft.AspNetCore.Http;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using Altinn.Correspondence.Core.Options;
using Microsoft.ApplicationInsights;

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
        var exceptionMessage = FormatExceptionMessage(exception, httpContext);

        // Log to Application Insights
        var properties = new Dictionary<string, string>
        {
            { "ExceptionType", exception.GetType().Name },
            { "Path", httpContext.Request.Path },
            { "Environment", hostEnvironment.EnvironmentName },
            { "System", "Correspondence" },
            { "StackTrace", exception.StackTrace ?? "No stack trace available" },
            { "InnerExceptionStackTrace", exception.InnerException?.StackTrace ?? "No inner exception stack trace" },
            { "ExceptionSource", "HTTP" },
            { "ExceptionIdentifier", $"{exception.GetType().Name}:{httpContext.Request.Path}" },
            { "ExceptionMessage", exception.Message },
            { "InnerExceptionType", exception.InnerException?.GetType().Name ?? "None" },
            { "InnerExceptionMessage", exception.InnerException?.Message ?? "None" },
            { "SentToSlack", "true" },
            { "SlackMessage", exceptionMessage }
        };

        logger.LogError(
            exception,
            "Unhandled exception occurred. Type: {ExceptionType}, Message: {Message}, Path: {Path}",
            exception.GetType().Name,
            exception.Message,
            httpContext.Request.Path);

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
            // Log Slack notification failure to Application Insights
            var slackProperties = new Dictionary<string, string>
            {
                { "OriginalExceptionType", exception.GetType().Name },
                { "SlackExceptionType", slackEx.GetType().Name },
                { "Environment", hostEnvironment.EnvironmentName },
                { "System", "Correspondence" },
                { "StackTrace", slackEx.StackTrace ?? "No stack trace available" },
                { "InnerExceptionStackTrace", slackEx.InnerException?.StackTrace ?? "No inner exception stack trace" },
                { "ExceptionSource", "SlackNotification" },
                { "ExceptionIdentifier", $"SlackNotification:{exception.GetType().Name}:{httpContext.Request.Path}" },
                { "ExceptionMessage", slackEx.Message },
                { "InnerExceptionType", slackEx.InnerException?.GetType().Name ?? "None" },
                { "InnerExceptionMessage", slackEx.InnerException?.Message ?? "None" }
            };

            logger.LogError(
                slackEx,
                "Failed to send Slack notification");
            return true;
        }
    }

    public async ValueTask<bool> TryHandleAsync(string jobId, string jobName, Exception exception, CancellationToken cancellationToken)
    {
        var exceptionMessage = FormatExceptionMessage(jobId, jobName, exception);

        // Log to Application Insights
        var properties = new Dictionary<string, string>
        {
            { "ExceptionType", exception.GetType().Name },
            { "JobId", jobId },
            { "JobName", jobName },
            { "Environment", hostEnvironment.EnvironmentName },
            { "System", "Correspondence" },
            { "StackTrace", exception.StackTrace ?? "No stack trace available" },
            { "InnerExceptionStackTrace", exception.InnerException?.StackTrace ?? "No inner exception stack trace" },
            { "ExceptionSource", "Job" },
            { "ExceptionIdentifier", $"{exception.GetType().Name}:{jobName}" },
            { "ExceptionMessage", exception.Message },
            { "InnerExceptionType", exception.InnerException?.GetType().Name ?? "None" },
            { "InnerExceptionMessage", exception.InnerException?.Message ?? "None" },
            { "SentToSlack", "true" },
            { "SlackMessage", exceptionMessage }
        };

        logger.LogError(
            exception,
            "Unhandled exception occurred. Job ID: {JobId}, Job Name: {JobName}, Type: {ExceptionType}, Message: {Message}",
            jobId,
            jobName,
            exception.GetType().Name,
            exception.Message);

        try
        {
            await SendSlackNotificationWithMessage(exceptionMessage);
            return true;
        }
        catch (Exception ex)
        {
            // Log Slack notification failure to Application Insights
            var slackProperties = new Dictionary<string, string>
            {
                { "OriginalExceptionType", exception.GetType().Name },
                { "SlackExceptionType", ex.GetType().Name },
                { "JobId", jobId },
                { "JobName", jobName },
                { "Environment", hostEnvironment.EnvironmentName },
                { "System", "Correspondence" },
                { "StackTrace", ex.StackTrace ?? "No stack trace available" },
                { "InnerExceptionStackTrace", ex.InnerException?.StackTrace ?? "No inner exception stack trace" },
                { "ExceptionSource", "JobSlackNotification" },
                { "ExceptionIdentifier", $"JobSlackNotification:{exception.GetType().Name}:{jobName}" },
                { "ExceptionMessage", ex.Message },
                { "InnerExceptionType", ex.InnerException?.GetType().Name ?? "None" },
                { "InnerExceptionMessage", ex.InnerException?.Message ?? "None" }
            };

            logger.LogError(ex, "Failed to send Slack notification");
            return false;
        }
    }   

    private string FormatExceptionMessage(Exception exception, HttpContext context)
    {
        return $":warning: *Unhandled Exception*\n" +
               $"*Environment:* {hostEnvironment.EnvironmentName}\n" +
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
                $"*Environment:* {hostEnvironment.EnvironmentName}\n" +
                $"*System:* Correspondence\n" +
                $"*Job ID:* {jobId}\n" +
                $"*Job Name:* {jobName}\n" +
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