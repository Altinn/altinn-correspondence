using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Diagnostics;
using Slack.Webhooks;
using Microsoft.AspNetCore.Http;
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using Altinn.Correspondence.Core.Options;

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
            logger.LogError(
                slackEx,
                "Failed to send Slack notification");
            return true;
        }
    }

    public async ValueTask<bool> TryHandleAsync(string jobId, string jobName, Exception exception, CancellationToken cancellationToken)
    {
        var exceptionMessage = FormatExceptionMessage(jobId, jobName, exception);

        logger.LogError(
            exception,
            "Unhandled exception occurred. Job ID: {JobId}, Job Name: {JobName}, Type: {ExceptionType}, Message: {Message}",
            jobId,
            jobName,
            exception.GetType().Name,
            exception.Message);

        var slackMessage = new SlackMessage
        {
            Text = exceptionMessage,
            Channel = Channel
        };

        try
        {
            await SendSlackNotificationWithMessage(exceptionMessage);
            return true;
        }
        catch (Exception ex)
        {
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