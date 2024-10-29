using Microsoft.AspNetCore.Diagnostics;
using Slack.Webhooks;

namespace Altinn.Correspondence.Helpers;
public class SlackExceptionNotification : IExceptionHandler
{
    private readonly ILogger<SlackExceptionNotification> logger;
    private readonly ISlackClient _slackClient;
    private const string TestChannel = "#test-varslinger";

    public SlackExceptionNotification(ILogger<SlackExceptionNotification> logger, ISlackClient slackClient)
    {
        this.logger = logger;
        _slackClient = slackClient;
    }
    public ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var exceptionMessage = "Error with status code 500 detected:\n" + exception.StackTrace;

        logger.LogError(
            "Error Message: {exceptionMessage}, Time of occurrence {time}",
            exceptionMessage, DateTime.UtcNow);
        
        SendSlackNotificationWithMessage(exceptionMessage);
        return ValueTask.FromResult(false);
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