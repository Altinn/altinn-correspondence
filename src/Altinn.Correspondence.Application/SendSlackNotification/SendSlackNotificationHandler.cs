using Altinn.Correspondence.Core.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Slack.Webhooks;

namespace Altinn.Correspondence.Application.SendSlackNotification;

/// <summary>
/// handler for sending Slack notifications.
/// </summary>
public class SendSlackNotificationHandler(
    ISlackClient slackClient,
    SlackSettings slackSettings,
    IHostEnvironment hostEnvironment,
    ILogger<SendSlackNotificationHandler> logger)
{
    public async Task Process(string title, string message)
    {
        var text =
            $":warning: *{title}*\n" +
            $"*Environment:* {hostEnvironment.EnvironmentName}\n" +
            $"*System:* Correspondence\n" +
            $"*Message:* {message}\n" +
            $"*Time:* {DateTime.UtcNow:u}\n";

        var slackMessage = new SlackMessage
        {
            Text = text,
            Channel = slackSettings.NotificationChannel
        };

        try
        {
            await slackClient.PostAsync(slackMessage);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send Slack notification. Title={Title}", title);
        }
    }
}