using Altinn.Correspondence.Core.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Slack.Webhooks;

public class SlackNotificationService(IConfiguration configuration,
    ISlackClient slackClient,
    IHostEnvironment hostEnvironment,
    SlackSettings slackSettings,
    ILogger<SlackNotificationService> logger)
{
    public async Task SendSlackMessage(string message)
    {
        logger.LogInformation($"Posting to Slack: {message}");
        var slackMessage = new SlackMessage
        {
            Text = $"Slack alert from {hostEnvironment.EnvironmentName}: {message}",
            Channel = slackSettings.NotificationChannel,
        };
        await slackClient.PostAsync(slackMessage);
    }
}
