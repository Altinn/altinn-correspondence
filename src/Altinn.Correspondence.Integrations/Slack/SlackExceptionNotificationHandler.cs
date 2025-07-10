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
        var exceptionMessage = FormatExceptionMessage(exception, httpContext);
        logger.LogError(
            exception,
            null);
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
                null);
            return true;
        }
    }

    public async ValueTask<bool> TryHandleAsync(string jobId, string jobName, Exception exception, int retryCount, CancellationToken cancellationToken)
    {
        // Only send Slack notification on every 3rd retry (3, 6, 9)
        if (retryCount % 3 != 0)
        {
            logger.LogInformation("Skipping Slack notification for job {JobId} on retry {RetryCount} (only posting every 3rd retry)", jobId, retryCount);
            return true;
        }

        var exceptionMessage = FormatExceptionMessage(jobId, jobName, exception, retryCount);
        logger.LogError(
            exception,
            null);

        try
        {
            await SendSlackNotificationWithMessage(exceptionMessage);
            return true;
        }
        catch (Exception slackEx)
        {
            logger.LogError(slackEx, null);
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

    private string FormatExceptionMessage(string jobId, string jobName, Exception exception, int retryCount)
    {
        return $":warning: *Unhandled Exception*\n" +
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